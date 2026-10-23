"""Load an ew pack artifact in the production Kernel and check development isolation."""
import http.cookiejar
import os
from pathlib import Path
import re
import secrets
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

if len(sys.argv) != 4:
    raise SystemExit('Usage: qualify-kernel.py <Kernel.dll> <artifact-directory> <new-output-directory>')
kernel, artifact, output = map(lambda value: Path(value).resolve(), sys.argv[1:])
output.mkdir(parents=True, exist_ok=False)
with socket.socket() as reservation:
    reservation.bind(('127.0.0.1', 0))
    port = reservation.getsockname()[1]
base = f'http://127.0.0.1:{port}'
environment = dict(os.environ)
environment.update({
    'ConnectionStrings__EnterpriseWorkflow': f'Data Source={output / "kernel.db"}',
    'Database__Provider': 'Sqlite',
    'Database__MigrationMode': 'Validate',
    'Modules__Directories__0': str(artifact),
    'ASPNETCORE_URLS': base,
    'DOTNET_ENVIRONMENT': 'Production',
    'ASPNETCORE_ENVIRONMENT': 'Production',
})
password = 'Qualification_' + secrets.token_hex(24)
for arguments in [('migrate',), ('bootstrap', 'qualification-admin', password)]:
    result = subprocess.run(['dotnet', str(kernel), *arguments], env=environment, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
log = (output / 'kernel.log').open('w')
process = subprocess.Popen(['dotnet', str(kernel)], env=environment, stdout=log, stderr=log)
jar = http.cookiejar.CookieJar()
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
def get(path):
    return client.open(base + path, timeout=3).read().decode()
try:
    for _ in range(100):
        if process.poll() is not None:
            raise RuntimeError((output / 'kernel.log').read_text()[-4000:])
        try:
            if 'Se connecter' in get('/login'):
                break
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(.2)
    else:
        raise RuntimeError('Kernel readiness timed out')
    for path in ['/__ew/dev', '/__ew/dev/identity']:
        try:
            client.open(base + path, timeout=3)
        except urllib.error.HTTPError as error:
            assert error.code == 404, error.code
        else:
            raise AssertionError('Development endpoint exposed by production Kernel')
    login = get('/login')
    fields = dict(re.findall(r'<input[^>]*name="([^"]+)"[^>]*value="([^"]*)"', login))
    fields.update(userName='qualification-admin', password=password)
    client.open(urllib.request.Request(base + '/auth/login', urllib.parse.urlencode(fields).encode()), timeout=5).read()
    administration = get('/admin/workflows')
    assert 'Demande de cong' in administration, 'Packaged process absent from Kernel catalogue'
    assert 'Examiner une demande aux RH' in administration, 'Module requirements absent from catalogue'
    assert '/__ew/dev' not in administration, 'Development selector present in production UI'
    assert '.app-shell' in get('/_content/EnterpriseWorkflow.Web/enterprise-workflow.css')
    print('Packaged LeaveRequest loaded by production Kernel; catalogue and CSS available; development endpoints absent', flush=True)
finally:
    process.terminate()
    try:
        process.wait(timeout=15)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
    log.close()
