#!/usr/bin/env python3
"""Build an explicit NuGet catalog; never publish or infer release approval."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def prepare(output, public):
    catalog = json.loads((ROOT / 'release/catalog.json').read_text())
    if public and not all(catalog.get(k) for k in ('authors', 'licenseExpression')):
        raise ValueError('Public preparation requires approved authors and licenseExpression in release/catalog.json')
    output.mkdir(parents=True, exist_ok=False)
    version = catalog['version']
    expected = {Path(p).stem for p in catalog['projects']}
    for project in catalog['projects']:
        command = ['dotnet', 'pack', str(ROOT / project), '-c', 'Release', '-o', str(output),
                   '-p:UseSharedCompilation=false', '-m:1', f'-p:PackageVersion={version}',
                   '-p:RepositoryType=git', f'-p:RepositoryUrl={catalog["repositoryUrl"]}',
                   '-p:PackageReadmeFile=README.md']
        if public:
            command += [f'-p:Authors={catalog["authors"]}',
                        f'-p:PackageLicenseExpression={catalog["licenseExpression"]}']
        subprocess.run(command, cwd=ROOT, check=True)
    records = []
    for package in sorted(output.glob('*.nupkg')):
        with zipfile.ZipFile(package) as archive:
            names = archive.namelist()
            nuspec, = [n for n in names if n.endswith('.nuspec')]
            metadata = ET.fromstring(archive.read(nuspec))
            def field(name):
                return metadata.findtext(f'.//{{*}}metadata/{{*}}{name}')
            package_id = field('id')
            if package_id not in expected or field('version') != version:
                raise ValueError(f'Unexpected identity/version: {package}')
            expected.remove(package_id)
            if not {'README.md', 'LICENSE'}.issubset(names):
                raise ValueError(f'Missing package README/license: {package}')
            for dependency in metadata.findall('.//{*}dependency'):
                if dependency.get('id', '').startswith('EnterpriseWorkflow.'):
                    if dependency.get('id') not in {Path(p).stem for p in catalog['projects']}:
                        raise ValueError(f'Dependency outside catalog: {package}: {dependency.attrib}')
                    if dependency.get('version') not in (version, f'[{version}, )', f'[{version}]'):
                        raise ValueError(f'Inconsistent dependency version: {package}: {dependency.attrib}')
            for name in names:
                parts = Path(name).parts
                if any(p in ('.git', '.ew', '.env', 'appsettings.Development.json') for p in parts) or name.endswith(('.pfx', '.key', '.db', '.sqlite')):
                    raise ValueError(f'Forbidden package content: {package}: {name}')
            if public and (field('authors') != catalog['authors'] or field('license') != catalog['licenseExpression']):
                raise ValueError(f'Incorrect public metadata: {package}')
        records.append({'file': package.name, 'id': package_id, 'version': version,
                        'sha256': hashlib.sha256(package.read_bytes()).hexdigest()})
    if expected:
        raise ValueError(f'Missing packages: {sorted(expected)}')
    manifest = {'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                'workingTreeDirty': bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT)),
                'sdk': subprocess.check_output(['dotnet', '--version'], cwd=ROOT, text=True).strip(),
                'publicMetadataChecked': public, 'packages': records}
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path, help='New output directory')
    parser.add_argument('--public', action='store_true', help='Require approved public metadata')
    args = parser.parse_args()
    prepare(args.output.resolve(), args.public)
