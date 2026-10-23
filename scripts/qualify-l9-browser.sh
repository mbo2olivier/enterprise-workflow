#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
if command -v cygpath >/dev/null 2>&1; then repo_root="$(cygpath -m "$repo_root")"; fi
qualification_root="${1:?Usage: qualify-l9-browser.sh <qualified-local-packages-directory>}"
qualification_root="$(cd "$qualification_root" && pwd)"
if command -v cygpath >/dev/null 2>&1; then qualification_root="$(cygpath -m "$qualification_root")"; fi
cli_path="$qualification_root/tools/ew"
base_url="http://127.0.0.1:5087"
compose_path="$repo_root/tests/smtp/docker-compose.yml"
runtime_pid=""
cleanup() {
  if [ -n "$runtime_pid" ]; then printf 'stop\n' >&3; wait "$runtime_pid" || true; fi
  exec 3>&-
  if [ -p "$qualification_root/dev.stdin" ]; then rm "$qualification_root/dev.stdin"; fi
  docker compose -f "$compose_path" down --volumes
}
mkfifo "$qualification_root/dev.stdin"
exec 3<> "$qualification_root/dev.stdin"
trap cleanup EXIT
docker compose -f "$compose_path" up -d --wait
python3 - "$repo_root" "$qualification_root" "$base_url" <<'PY'
import json,sys,shutil,xml.etree.ElementTree as ET
from pathlib import Path
repo,output,url=map(str,sys.argv[1:])
source=Path(repo)/'samples/LeaveRequest.Module'
sample=Path(output)/'LeaveRequest.Module'
sample.mkdir()
for file in source.iterdir():
 if file.is_file() and (file.suffix in ('.cs','.razor','.csproj') or file.name == 'ew.dev.json'):
  shutil.copy2(file,sample/file.name)
project=next(sample.glob('*.csproj'))
tree=ET.parse(project)
for reference in tree.findall('.//ProjectReference'):
 reference.tag='PackageReference'
 reference.set('Include',Path(reference.get('Include')).stem)
 reference.set('Version','1.0.0')
properties=tree.getroot().find('PropertyGroup')
for name,value in [('TargetFramework','net10.0'),('ImplicitUsings','enable'),('Nullable','enable')]:
 ET.SubElement(properties,name).text=value
tree.write(project,encoding='unicode')
s=json.loads((sample/'ew.dev.json').read_text())
s['storage']={'provider':'sqlite','path':str(sample/'.ew/dev/workflow.db')}
s['smtp']={'host':'127.0.0.1','port':1025,'enableSsl':False,'from':'workflow@example.test','baseUrl':url,'pageSize':1,'addresses':{'local|ew-dev-bob':'bob@example.test','local|ew-dev-carol':'carol@example.test'}}
(Path(output)/'ew.smtp.json').write_text(json.dumps(s))
PY
"$cli_path" pack "$qualification_root/LeaveRequest.Module" --configuration Release --output "$qualification_root/leave-artifacts"
"$cli_path" verify "$qualification_root/leave-artifacts/sample.leave-request/1.0.0"
"$cli_path" dev "$qualification_root/LeaveRequest.Module" --config "$qualification_root/ew.smtp.json" --configuration Release --no-watch --url "$base_url" < "$qualification_root/dev.stdin" > "$qualification_root/dev.log" 2>&1 &
runtime_pid=$!
for attempt in {1..60}; do
  if curl --fail --silent "$base_url/__ew/dev" >/dev/null; then break; fi
  if ! kill -0 "$runtime_pid" 2>/dev/null || [ "$attempt" -eq 60 ]; then cat "$qualification_root/dev.log"; exit 1; fi
  sleep 1
done
EW_DEV_TEST_URL="$base_url" EW_SMTP_TEST_URL="http://127.0.0.1:8025" EW_SMTP_COMPOSE_FILE="$compose_path" EW_DEV_TEST_DB="$qualification_root/LeaveRequest.Module/.ew/dev/workflow.db" \
  dotnet test --project "$repo_root/tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj" --configuration Release --no-build --no-restore -- --filter-method '*LeaveRequest*'

python3 "$repo_root/tests/cli/qualify-kernel.py" "$repo_root/src/EnterpriseWorkflow.Kernel/bin/Release/net10.0/EnterpriseWorkflow.Kernel.dll" "$qualification_root/leave-artifacts/sample.leave-request/1.0.0" "$qualification_root/kernel-qualification"
