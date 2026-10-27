#!/usr/bin/env python3
"""Validate release version alignment and, in CI, tag provenance."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def check(tag=None):
    catalog = json.loads((ROOT / 'release/catalog.json').read_text())
    version = catalog['version']
    if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?', version):
        raise ValueError('Invalid release version')
    if ET.parse(ROOT / 'Directory.Build.props').findtext('.//Version') != version:
        raise ValueError('Directory.Build.props version differs from catalog')
    for project in (ROOT / 'templates').glob('*/WorkflowSample.csproj'):
        for reference in ET.parse(project).findall('.//PackageReference'):
            if reference.get('Include', '').startswith('EnterpriseWorkflow.') and reference.get('Version') != version:
                raise ValueError(f'Template version mismatch: {project}')
    if f'# Enterprise Workflow {version}\n' not in (ROOT / 'release/NOTES.md').read_text():
        raise ValueError('Release notes version differs from catalog')
    if tag:
        if tag != f'v{version}':
            raise ValueError('Tag must equal v + catalog version')
        subprocess.run(['git', 'fetch', 'origin', 'main'], cwd=ROOT, check=True)
        subprocess.run(['git', 'merge-base', '--is-ancestor', 'HEAD', 'origin/main'], cwd=ROOT, check=True)
    print(f'Release {version}; NuGet owner: {catalog["nugetOwner"]}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tag')
    check(parser.parse_args().tag)
