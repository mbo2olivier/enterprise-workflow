#!/usr/bin/env python3
"""Exercise an extracted native Kernel archive and a cold SQLite backup/restore."""
import argparse
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('payload', type=Path)
parser.add_argument('--module', type=Path, required=True)
args = parser.parse_args()
payload = args.payload.resolve()
extraction = None
if payload.is_file():
    extraction = tempfile.TemporaryDirectory(prefix='ew-extracted-')
    shutil.unpack_archive(str(payload), extraction.name)
    payload, = Path(extraction.name).iterdir()
manifest = json.loads((payload / 'manifest.json').read_text())
for name, digest in manifest['files'].items():
    if hashlib.sha256((payload / name).read_bytes()).hexdigest() != digest:
        raise ValueError(f'Archive integrity mismatch: {name}')
executable = payload / ('EnterpriseWorkflow.Kernel.exe' if os.name == 'nt' else 'EnterpriseWorkflow.Kernel')
with tempfile.TemporaryDirectory(prefix='ew-kernel-') as directory:
    root = Path(directory)
    database = root / 'workflow.db'
    env = dict(os.environ, Database__Provider='Sqlite', Database__MigrationMode='Validate',
               ConnectionStrings__EnterpriseWorkflow=f'Data Source={database}',
               Modules__Directories__0=str(args.module.resolve()))
    subprocess.run([str(executable), 'migrate'], env=env, check=True, timeout=90)
    subprocess.run([str(executable), 'bootstrap', 'release-admin', 'Release-only-test-Password-2026!'],
                   env=env, check=True, timeout=90)

    def probe():
        with socket.socket() as listener:
            listener.bind(('127.0.0.1', 0))
            port = listener.getsockname()[1]
        env['ASPNETCORE_URLS'] = f'http://127.0.0.1:{port}'
        with (root / 'kernel.log').open('w+') as log:
            process = subprocess.Popen([str(executable)], env=env, stdout=log, stderr=subprocess.STDOUT)
            try:
                for _ in range(90):
                    if process.poll() is not None:
                        break
                    try:
                        with urllib.request.urlopen(env['ASPNETCORE_URLS'], timeout=1) as response:
                            if response.status == 200:
                                return
                    except (OSError, urllib.error.URLError):
                        time.sleep(1)
                log.seek(0)
                raise RuntimeError('Kernel did not serve HTTP:\n' + log.read())
            finally:
                process.terminate()
                try:
                    process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()

    probe()  # Loads the external module and reconciles its definitions.
    with closing(sqlite3.connect(database)) as source, closing(sqlite3.connect(root / 'backup.db')) as backup:
        source.backup(backup)
        before = source.execute('SELECT name FROM sqlite_master WHERE type="table" ORDER BY name').fetchall()
        assert before
        before_dump = list(source.iterdump())
        assert any(line.startswith('INSERT INTO') for line in before_dump), 'Expected persisted bootstrap/module data'
    database.unlink()
    for suffix in ('-wal', '-shm'):
        Path(str(database) + suffix).unlink(missing_ok=True)
    shutil.copy2(root / 'backup.db', database)
    with closing(sqlite3.connect(database)) as restored:
        assert restored.execute('PRAGMA integrity_check').fetchone() == ('ok',)
        assert restored.execute('SELECT name FROM sqlite_master WHERE type="table" ORDER BY name').fetchall() == before
        assert list(restored.iterdump()) == before_dump, 'Restored data differs from backup source'
    probe()
print('Native Kernel, external module, SQLite migration and backup/restore: OK')
if extraction:
    extraction.cleanup()
