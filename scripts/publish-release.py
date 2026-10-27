#!/usr/bin/env python3
"""Publish a verified inventory; resume only when existing content is identical."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def contents(data):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        return {name: hashlib.sha256(archive.read(name)).hexdigest()
                for name in archive.namelist() if name != '.signature.p7s'}


def remote(package_id, version):
    name, version = package_id.lower(), version.lower()
    url = f'https://api.nuget.org/v3-flatcontainer/{name}/{version}/{name}.{version}.nupkg'
    try:
        with urllib.request.urlopen(url, timeout=60) as response:
            return response.read()
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise


def inventory(folder):
    manifest = json.loads((folder / 'manifest.json').read_text())
    catalog = json.loads((ROOT / 'release/catalog.json').read_text())
    expected = {Path(p).stem for p in catalog['projects']}
    if not manifest['publicMetadataChecked'] or manifest['workingTreeDirty']:
        raise ValueError('Publication requires a clean, public-metadata-checked build')
    if os.environ.get('GITHUB_SHA') and manifest['sourceCommit'] != os.environ['GITHUB_SHA']:
        raise ValueError('Artifact commit differs from workflow commit')
    records = {}
    for record in manifest['packages']:
        if Path(record['file']).name != record['file'] or record['id'] in records:
            raise ValueError('Invalid inventory path or duplicate identity')
        file = folder / record['file']
        if record['version'] != catalog['version']:
            raise ValueError('Manifest version differs from release catalog')
        if hashlib.sha256(file.read_bytes()).hexdigest() != record['sha256']:
            raise ValueError(f'Artifact hash mismatch: {file}')
        with zipfile.ZipFile(file) as archive:
            name, = [n for n in archive.namelist() if n.endswith('.nuspec')]
            metadata = ET.fromstring(archive.read(name))
            if metadata.findtext('.//{*}metadata/{*}id') != record['id'] or metadata.findtext('.//{*}metadata/{*}version') != record['version']:
                raise ValueError('Inventory and package metadata differ')
            record['dependencies'] = {node.get('id') for node in metadata.findall('.//{*}dependency')
                                      if node.get('id', '').startswith('EnterpriseWorkflow.')}
        records[record['id']] = record
    if set(records) != expected:
        raise ValueError('Manifest does not contain the exact release catalog')
    libraries = {key for key in records if not key.endswith(('.Cli', '.Templates'))}
    for key, record in records.items():
        if key.endswith(('.Cli', '.Templates')):
            record['dependencies'].update(libraries)
    pending, ordered = dict(records), []
    while pending:
        ready = sorted(key for key, record in pending.items() if not record['dependencies'].intersection(pending))
        if not ready:
            raise ValueError('Cyclic package dependencies')
        # Tools/templates follow the libraries even when tool payload embeds them.
        ready.sort(key=lambda key: (key.endswith(('.Cli', '.Templates')), key))
        for key in ready:
            ordered.append(pending.pop(key))
    return ordered


def publish(folder, wait_only=False):
    records = inventory(folder)
    if not wait_only:
        if not os.environ.get('NUGET_API_KEY'):
            raise ValueError('NUGET_API_KEY must be injected by the protected publishing job')
        # Preflight every existing version before making any mutation.
        existing = {}
        for record in records:
            data = remote(record['id'], record['version'])
            if data is not None and contents(data) != contents((folder / record['file']).read_bytes()):
                raise ValueError(f'Existing package differs: {record["id"]}; use a new version')
            existing[record['id']] = data is not None
        for record in records:
            if existing[record['id']]:
                print(f'Identical package already published: {record["id"]}', flush=True)
                continue
            subprocess.run(['dotnet', 'nuget', 'push', str(folder / record['file']),
                            '--source', 'https://api.nuget.org/v3/index.json'], check=True)
    for attempt in range(40):
        missing = []
        for record in records:
            data = remote(record['id'], record['version'])
            if data is None:
                missing.append(record['id'])
            elif contents(data) != contents((folder / record['file']).read_bytes()):
                raise ValueError(f'Public content mismatch: {record["id"]}')
        if not missing:
            print('All public package contents verified')
            return
        print(f'Waiting for NuGet indexing ({attempt + 1}/40): {missing}', flush=True)
        time.sleep(15)
    raise TimeoutError('Public packages not yet available; rerun the same tag workflow')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', type=Path)
    parser.add_argument('--wait-only', action='store_true')
    args = parser.parse_args()
    publish(args.folder.resolve(), args.wait_only)
