#!/usr/bin/env python3
"""Opt-in heavy L12 qualification. Each phase is explicit; never publishes packages."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parents[1]
SAMPLE = REPO / 'samples/OrganizationWorkflow'


def require_package(feed, package_id, version):
    package = feed / f'{package_id}.{version}.nupkg'
    if not package.is_file():
        raise ValueError(f'Packaging produced no expected package: {package}. Check IsPackable=true in the project.')
    with zipfile.ZipFile(package) as archive:
        nuspecs = [name for name in archive.namelist() if name.endswith('.nuspec')]
        if len(nuspecs) != 1:
            raise ValueError(f'Expected one nuspec in {package}')
        metadata = ET.fromstring(archive.read(nuspecs[0]))
        actual_id = metadata.findtext('.//{*}metadata/{*}id')
        actual_version = metadata.findtext('.//{*}metadata/{*}version')
        if (actual_id, actual_version) != (package_id, version):
            raise ValueError(f'Unexpected package identity: {actual_id} {actual_version}; expected {package_id} {version}')
    return package


def write_qualification_nuget_config(root):
    config = ET.Element('configuration')
    settings = ET.SubElement(config, 'config')
    ET.SubElement(settings, 'add', key='globalPackagesFolder', value=str(root / 'packages'))
    sources = ET.SubElement(config, 'packageSources'); ET.SubElement(sources, 'clear')
    ET.SubElement(sources, 'add', key='l12-local', value=str(root / 'feed'))
    ET.SubElement(sources, 'add', key='nuget.org', value='https://api.nuget.org/v3/index.json')
    mapping = ET.SubElement(config, 'packageSourceMapping'); ET.SubElement(mapping, 'clear')
    local = ET.SubElement(mapping, 'packageSource', key='l12-local')
    for pattern in ['EnterpriseWorkflow.*', 'Example.Workflow.*']:
        ET.SubElement(local, 'package', pattern=pattern)
    public = ET.SubElement(mapping, 'packageSource', key='nuget.org')
    ET.SubElement(public, 'package', pattern='*')
    ET.ElementTree(config).write(root / 'NuGet.Config', encoding='utf-8', xml_declaration=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('phase', choices=['configure', 'packages', 'install', 'generate', 'artifact', 'kernel', 'incompatible'])
    parser.add_argument('workspace', type=Path)
    args = parser.parse_args()
    root = args.workspace.resolve()
    print(f"Workspace (physical path; use this value for EW_L12_ROOT): {root}", flush=True)
    if root == REPO or REPO in root.parents:
        raise ValueError('Use a dedicated workspace outside the repository.')
    root.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, NUGET_PACKAGES=str(root / 'packages'), DOTNET_CLI_HOME=str(root / 'dotnet-home'), Templates__Root=str(root / 'template-hives'))
    def run(command, cwd=root, expected=0, expected_text=None):
        print('+', ' '.join(map(str, command)), flush=True)
        result = subprocess.run(list(map(str, command)), cwd=cwd, env=env, check=False, text=True, capture_output=expected_text is not None)
        if expected_text is not None:
            print(result.stdout + result.stderr, end='')
            if expected_text not in result.stdout + result.stderr:
                raise RuntimeError(f'Missing expected diagnostic: {expected_text}')
        if result.returncode != expected:
            raise RuntimeError(f'Expected exit {expected}, received {result.returncode}')
    cli = root / 'tools' / ('few-example.exe' if os.name == 'nt' else 'few-example')
    distribution = json.loads((SAMPLE / 'distribution.json').read_text())
    def install_distribution():
        for name in ['Contracts', 'Sdk', 'Hosting', 'Cli', 'Templates']:
            package_id = f'Example.Workflow.{name}'
            require_package(root / 'feed', package_id, distribution['packages'][package_id])
        run(['dotnet', 'tool', 'install', 'Example.Workflow.Cli', '--version', distribution['packages']['Example.Workflow.Cli'], '--tool-path', root / 'tools', '--configfile', root / 'NuGet.Config'])
        run([cli, 'distribution'])
        shutil.copyfile(SAMPLE / 'distribution.json', root / 'distribution.json')
    if args.phase == 'packages':
        feed = root / 'feed'
        if feed.exists():
            raise ValueError('The feed already exists; use a fresh workspace for packaging.')
        feed.mkdir()
        (root / 'revision.txt').write_text(subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=REPO, text=True))
        write_qualification_nuget_config(root)
        projects = json.loads((REPO / 'release/catalog.json').read_text())['projects']
        for project in projects:
            run(['dotnet', 'pack', REPO / project, '-c', 'Release', '-o', feed, '-m:1', '-p:UseSharedCompilation=false'], cwd=REPO)
            require_package(feed, Path(project).stem, json.loads((REPO / 'release/catalog.json').read_text())['version'])
        for name in ['Contracts', 'Sdk', 'Hosting', 'Cli', 'Templates']:
            run(['dotnet', 'pack', SAMPLE / name / f'Example.Workflow.{name}.csproj', '-c', 'Release', '-o', feed, '-m:1', '-p:UseSharedCompilation=false'], cwd=REPO)
            package_id = f'Example.Workflow.{name}'
            require_package(feed, package_id, distribution['packages'][package_id])
        install_distribution()
    elif args.phase == 'configure':
        if not (root / 'feed').is_dir():
            raise ValueError('The qualification feed is missing.')
        write_qualification_nuget_config(root)
    elif args.phase == 'install':
        install_distribution()
    elif args.phase == 'generate':
        if (root / 'Generated').exists():
            raise ValueError('Generated already exists; preserve the previous result and use a new workspace.')
        # A fresh isolated template hive is expected. This invocation must fail before setup.
        run([cli, 'new', 'few-example-module', '-n', 'Generated'], expected=1, expected_text='setup')
        run([cli, 'setup'])
        run([cli, 'new', 'few-example-module', '-n', 'Generated'])
        run(['dotnet', 'restore', root / 'Generated', '--configfile', root / 'NuGet.Config', '--use-lock-file'])
        (root / 'Generated' / '.env').write_text('Example__PolicyUrl=http://127.0.0.1:5099\nLocalization__DefaultCulture=en\n')
    elif args.phase == 'artifact':
        run([cli, 'validate', root / 'Generated', '--configuration', 'Release'])
        run([cli, 'inspect', root / 'Generated', '--configuration', 'Release', '--format', 'json', '--output', root / 'preview.json'])
        run([cli, 'pack', root / 'Generated', '--configuration', 'Release', '--output', root / 'artifacts'])
        manifests = list((root / 'artifacts').glob('**/module.json'))
        if len(manifests) != 1:
            raise ValueError(f'Expected one artifact, found {len(manifests)}')
        artifact = manifests[0].parent
        run([cli, 'verify', artifact])
        manifest = json.loads(manifests[0].read_text())
        if manifest['runtime'] != {'id': 'Example.Workflow', 'versionRange': '[1.0.0-rc.1,2.0.0)'}:
            raise ValueError('Unexpected runtime compatibility declaration.')
        if any(path.name == '.env' or path.suffix in ('.db', '.sqlite') for path in artifact.rglob('*')):
            raise ValueError('A local configuration/database leaked into the artifact.')
        (root / 'artifact-path.txt').write_text(str(artifact))
    elif args.phase == 'kernel':
        artifact = Path((root / 'artifact-path.txt').read_text())
        run(['dotnet', 'publish', SAMPLE / 'Kernel/Example.Workflow.Kernel.csproj', '-c', 'Release', '-o', root / 'kernel', '-m:1', '-p:UseSharedCompilation=false'], cwd=REPO)
        settings = {'Database': {'Provider': 'Sqlite', 'MigrationMode': 'Validate'},
                    'ConnectionStrings': {'EnterpriseWorkflow': 'Data Source=' + str(root / 'kernel.db')},
                    'Modules': {'RootDirectory': str(root / 'artifacts'), 'Directories': [str(root / 'ignored-individual-path')]},
                    'Example': {'PolicyUrl': 'http://127.0.0.1:5099'},
                    'Localization': {'DefaultCulture': 'fr', 'FallbackCulture': 'en', 'SupportedCultures': ['fr', 'en']}}
        (root / 'kernel/appsettings.json').write_text(json.dumps(settings, indent=2))
        run(['dotnet', root / 'kernel/Example.Workflow.Kernel.dll', 'migrate'])
    else:
        artifact = Path((root / 'artifact-path.txt').read_text())
        run(['dotnet', 'run', '--project', REPO / 'src/EnterpriseWorkflow.Cli', '-c', 'Release', '--no-build', '--', 'verify', artifact], expected=1, expected_text='EW7055_INCOMPATIBLE_RUNTIME')
        print('Expected: EW7055_INCOMPATIBLE_RUNTIME (EnterpriseWorkflow cannot host an Example.Workflow module).')
    print(f'L12 phase {args.phase}: completed. Preserve logs and workspace {root}.')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, RuntimeError, OSError) as error:
        print(error, file=sys.stderr)
        sys.exit(1)
