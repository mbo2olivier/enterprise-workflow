#!/usr/bin/env python3
"""Prepare disposable L13 Kernel configurations after publishing both hosts."""
import argparse
import json
from pathlib import Path
import sys
import subprocess

HOSTS = (
    ('kernel', 'EnterpriseWorkflow.Kernel.dll', 'modules', 'standard.db'),
    ('custom-kernel', 'Example.Workflow.Kernel.dll', 'custom-modules', 'custom.db'),
)


def resolve_workspace(workspace):
    if not str(workspace).strip():
        raise ValueError('Provide the existing L13 qualification workspace.')
    root = Path(workspace).resolve(strict=True)
    repository = Path(__file__).resolve().parents[1]
    if root == repository or repository in root.parents:
        raise ValueError('Use a qualification workspace outside the repository.')
    return root


def prepare(workspace):
    root = resolve_workspace(workspace)
    # Validate both hosts before changing either configuration.
    for host, assembly, _, _ in HOSTS:
        directory = root / host
        if not (directory / assembly).is_file():
            raise ValueError(f'Published host missing: {directory / assembly}. Publish both hosts first.')
        backup = directory / 'appsettings.before-l13.json'
        if backup.exists():
            raise ValueError(f'Backup already exists: {backup}. Existing configurations were left unchanged.')
    for host, _, modules, database in HOSTS:
        directory = root / host
        path = directory / 'appsettings.json'
        config = {
            'Database': {'Provider': 'Sqlite', 'MigrationMode': 'Validate'},
            'ConnectionStrings': {'EnterpriseWorkflow': f'Data Source={root / database}'},
            'Modules': {
                'RootDirectory': f'../{modules}',
                'Directories': [str(root / 'this-individual-path-must-be-ignored')],
                'Discovery': {'MaximumDepth': 8, 'MaximumDirectories': 10000,
                              'MaximumCandidates': 1024, 'MaximumEntries': 100000}},
            'Localization': {'DefaultCulture': 'fr', 'FallbackCulture': 'en',
                             'SupportedCultures': ['fr', 'en']},
        }
        if host == 'custom-kernel':
            config['Example'] = {'PolicyUrl': 'http://127.0.0.1:5099'}
        if path.exists():
            path.rename(directory / 'appsettings.before-l13.json')
        path.write_text(json.dumps(config, indent=2) + '\n', encoding='utf-8')
        print(f'Configuration created: {path}')
    print('Configurations ready. Continue with database migration and bootstrap in the L13 recipe.')


def refresh_hosts(workspace):
    root = resolve_workspace(workspace)
    repository = Path(__file__).resolve().parents[1]
    saved = {}
    for host, assembly, _, _ in HOSTS:
        directory = root / host
        if not (directory / assembly).is_file() or not (directory / 'appsettings.json').is_file():
            raise ValueError(f'Existing published host/configuration missing: {directory}')
        saved[host] = {path.name: path.read_bytes() for path in directory.glob('appsettings*.json') if path.is_file()}
    projects = {
        'kernel': repository / 'src/EnterpriseWorkflow.Kernel/EnterpriseWorkflow.Kernel.csproj',
        'custom-kernel': repository / 'samples/OrganizationWorkflow/Kernel/Example.Workflow.Kernel.csproj',
    }
    for host, project in projects.items():
        try:
            subprocess.run(['dotnet', 'publish', str(project), '-c', 'Release', '--no-restore',
                            '-o', str(root / host), '-m:1', '-p:UseSharedCompilation=false'], cwd=repository, check=True)
        finally:
            # Publishing the standard Kernel copies its default appsettings.json.
            # Restore the qualification settings even when publishing fails.
            for name, content in saved[host].items():
                (root / host / name).write_bytes(content)
        print(f'Host refreshed; existing configuration preserved: {root / host}')
    print('Hosts refreshed. Databases and module artifacts were not changed.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--refresh-hosts', action='store_true', help='Republish both stopped hosts while preserving their existing JSON configurations.')
    parser.add_argument('workspace', help='Existing workspace containing kernel/ and custom-kernel/.')
    args = parser.parse_args()
    try:
        if args.refresh_hosts:
            refresh_hosts(args.workspace)
        else:
            prepare(args.workspace)
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print(error, file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
