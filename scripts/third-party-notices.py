#!/usr/bin/env python3
"""Inventory restored NuGet dependencies and preserve their supplied license texts."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('output', type=Path)
parser.add_argument('--rid-lock', type=Path)
parser.add_argument('--dotnet-notices', action='store_true')
args = parser.parse_args()
cache = os.environ.get('NUGET_PACKAGES')
if not cache:
    cache = subprocess.check_output(['dotnet', 'nuget', 'locals', 'global-packages', '--list'], text=True).strip().split(': ', 1)[1]
cache = Path(cache)
locks = list((ROOT / 'src').glob('*/packages.lock.json'))
if args.rid_lock:
    locks.append(args.rid_lock)
dependencies = set()
for lock in locks:
    for framework in json.loads(lock.read_text())['dependencies'].values():
        for name, details in framework.items():
            if details['type'] != 'Project':
                dependencies.add((name, details['resolved']))
args.output.mkdir(parents=True, exist_ok=True)
records = []
for name, version in sorted(dependencies):
    directory = cache / name.lower() / version.lower()
    nuspec = directory / f'{name.lower()}.nuspec'
    metadata = ET.parse(nuspec)
    license_node = metadata.find('.//{*}metadata/{*}license')
    record = {'id': name, 'version': version,
              'copyright': metadata.findtext('.//{*}copyright'),
              'license': license_node.text if license_node is not None else None,
              'licenseType': license_node.get('type') if license_node is not None else None,
              'licenseUrl': metadata.findtext('.//{*}licenseUrl')}
    if record['licenseType'] == 'file':
        source = (directory / record['license']).resolve()
        if not source.is_relative_to(directory.resolve()):
            raise ValueError(f'License path escapes package: {name}')
        destination = args.output / f'{name}.{version}.license.txt'
        destination.write_bytes(source.read_bytes())
        record['licenseFile'] = destination.name
        record['licenseSha256'] = hashlib.sha256(destination.read_bytes()).hexdigest()
    if not record['license'] and not record['licenseUrl']:
        raise ValueError(f'No license metadata: {name} {version}')
    records.append(record)
(args.output / 'dependencies.json').write_text(json.dumps(records, indent=2) + '\n')
lines = ['# Third-party NuGet dependency inventory', '',
         'Includes runtime and build dependencies from locked source projects.',
         'Dependencies retain their own licenses. This inventory is not a relicensing statement.', '',
         '| Package | Version | License |', '| --- | --- | --- |']
for record in records:
    license_text = record.get('licenseFile') or record['license'] or record['licenseUrl']
    lines.append(f'| {record["id"]} | {record["version"]} | {license_text} |')
(args.output / 'README.md').write_text('\n'.join(lines) + '\n')
if args.dotnet_notices:
    sdk_version = subprocess.check_output(['dotnet', '--version'], text=True).strip()
    sdk_line, = [line for line in subprocess.check_output(['dotnet', '--list-sdks'], text=True).splitlines()
                 if line.startswith(sdk_version + ' ')]
    sdk_root = Path(sdk_line.split('[', 1)[1].rstrip(']')).parent
    candidates = (sdk_root, sdk_root.parent / 'share/doc/dotnet')
    for filename in ('LICENSE.txt', 'ThirdPartyNotices.txt'):
        source = next((file for directory in candidates if directory.is_dir()
                       for file in directory.iterdir() if file.name.lower() == filename.lower()), None)
        if source is None:
            raise ValueError(f'Missing installed .NET redistribution notice: {filename}')
        (args.output / f'dotnet-{filename}').write_bytes(source.read_bytes())
    with (args.output / 'README.md').open('a') as readme:
        readme.write(f'\nBundled .NET SDK {sdk_version} redistribution license and third-party notices\n'
                     'are preserved as dotnet-LICENSE.txt and dotnet-ThirdPartyNotices.txt.\n')
print(f'Inventoried {len(records)} third-party NuGet dependencies')
