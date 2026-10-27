#!/usr/bin/env python3
"""Publish a candidate Kernel archive for one RID, without claiming qualification."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('rid', choices=json.loads((ROOT / 'release/catalog.json').read_text())['candidateRids'])
parser.add_argument('output', type=Path, help='New output directory')
parser.add_argument('--self-contained', action='store_true')
args = parser.parse_args()
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=False)
catalog = json.loads((ROOT / 'release/catalog.json').read_text())
mode = 'self-contained' if args.self_contained else 'framework-dependent'
name = f'EnterpriseWorkflow.Kernel-{catalog["version"]}-{args.rid}-{mode}'
payload = output / name
subprocess.run(['dotnet', 'publish', str(ROOT / 'src/EnterpriseWorkflow.Kernel'), '-c', 'Release',
                '-r', args.rid, '--self-contained', str(args.self_contained).lower(),
                f'-p:NuGetLockFilePath=obj/release-{args.rid}-{mode}.packages.lock.json',
                '-p:PublishTrimmed=false', '-p:PublishAot=false', '-p:UseSharedCompilation=false',
                f'-p:Version={catalog["version"]}', '-o', str(payload)], cwd=ROOT, check=True)
# Web SDK publishes development settings by default; these never belong in a release.
for file in payload.rglob('appsettings.Development.json'):
    file.unlink()
for source in ('LICENSE', 'release/PACKAGE-README.md'):
    shutil.copy2(ROOT / source, payload / Path(source).name)
subprocess.run([sys.executable, str(ROOT / 'scripts/third-party-notices.py'),
                str(payload / 'third-party'), '--dotnet-notices', '--rid-lock',
                str(ROOT / f'src/EnterpriseWorkflow.Kernel/obj/release-{args.rid}-{mode}.packages.lock.json')],
               cwd=ROOT, check=True)
manifest = {'rid': args.rid, 'mode': mode, 'version': catalog['version'], 'qualified': False,
            'commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
            'sdk': subprocess.check_output(['dotnet', '--version'], cwd=ROOT, text=True).strip(),
            'workingTreeDirty': bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT)),
            'files': {str(p.relative_to(payload)): hashlib.sha256(p.read_bytes()).hexdigest()
                      for p in sorted(payload.rglob('*')) if p.is_file()}}
(payload / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
archive = Path(shutil.make_archive(str(output / name), 'zip' if args.rid.startswith('win-') else 'gztar', output, name))
(output / 'SHA256SUMS').write_text(f'{hashlib.sha256(archive.read_bytes()).hexdigest()}  {archive.name}\n')
