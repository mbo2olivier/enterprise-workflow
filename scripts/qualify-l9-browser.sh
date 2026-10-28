#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
if command -v cygpath >/dev/null 2>&1; then repo_root="$(cygpath -m "$repo_root")"; fi
qualification_root="${1:?Usage: qualify-l9-browser.sh <qualified-local-packages-directory>}"
qualification_root="$(cd "$qualification_root" && pwd)"
if command -v cygpath >/dev/null 2>&1; then qualification_root="$(cygpath -m "$qualification_root")"; fi
package_root="$qualification_root"
if [ ! -d "$package_root/feed" ]; then
  echo "Packages/outils absents : lancer qualify-l9.sh avant ce script." >&2
  exit 1
fi
mkdir -p "$package_root/browser-runs"
qualification_root="$(mktemp -d "$package_root/browser-runs/run-XXXXXXXX")"
if command -v cygpath >/dev/null 2>&1; then qualification_root="$(cygpath -m "$qualification_root")"; fi
echo "Résultats de cette exécution : $qualification_root"
base_url="http://127.0.0.1:5087"
compose_path="$repo_root/tests/smtp/docker-compose.yml"
runtime_pid=""
cleanup() {
  if [ -n "$runtime_pid" ]; then printf 'stop\n' >&3; wait "$runtime_pid" || true; fi
  exec 3>&-
  if [ -p "$qualification_root/dev.stdin" ]; then rm "$qualification_root/dev.stdin"; fi
  docker compose -f "$compose_path" down --volumes
}
python3 - "$repo_root" "$qualification_root" "$base_url" "$package_root" <<'PY'
import json,sys,shutil,xml.etree.ElementTree as ET
from pathlib import Path
repo,output,url,packages=map(str,sys.argv[1:])
config=ET.Element('configuration')
sources=ET.SubElement(config,'packageSources')
ET.SubElement(sources,'clear')
ET.SubElement(sources,'add',key='local',value=str(Path(packages)/'feed'))
ET.SubElement(sources,'add',key='nuget.org',value='https://api.nuget.org/v3/index.json')
mapping=ET.SubElement(config,'packageSourceMapping')
for key,pattern in [('local','EnterpriseWorkflow.*'),('nuget.org','*')]:
 ET.SubElement(ET.SubElement(mapping,'packageSource',key=key),'package',pattern=pattern)
ET.SubElement(ET.SubElement(config,'config'),'add',key='globalPackagesFolder',value=str(Path(output)/'packages'))
ET.ElementTree(config).write(Path(output)/'NuGet.Config',encoding='unicode')
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
 reference.set('Version',json.loads((Path(repo)/'release/catalog.json').read_text())['version'])
properties=tree.getroot().find('PropertyGroup')
for name,value in [('TargetFramework','net10.0'),('ImplicitUsings','enable'),('Nullable','enable')]:
 ET.SubElement(properties,name).text=value
tree.write(project,encoding='unicode')
s=json.loads((sample/'ew.dev.json').read_text())
s['storage']={'provider':'sqlite','path':str(sample/'.ew/environments/default/workflow.db')}
s['smtp']={'host':'127.0.0.1','port':1025,'enableSsl':False,'from':'workflow@example.test','baseUrl':url,'pageSize':1,'addresses':{'local|ew-dev-bob':'bob@example.test','local|ew-dev-carol':'carol@example.test'}}
(Path(output)/'ew.smtp.json').write_text(json.dumps(s))
PY
# The CLI tool embeds Web DLLs. A fresh NuGet cache for the module does not
# update an already installed tool, even when package versions are identical.
dotnet pack "$repo_root/src/EnterpriseWorkflow.Cli/EnterpriseWorkflow.Cli.csproj" --configuration Release --output "$qualification_root/tool-feed" -p:UseSharedCompilation=false -m:1
python3 - "$qualification_root" <<'PY'
import sys,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(sys.argv[1])
config=ET.Element('configuration')
sources=ET.SubElement(config,'packageSources')
ET.SubElement(sources,'clear')
ET.SubElement(sources,'add',key='local',value=str(root/'tool-feed'))
ET.SubElement(ET.SubElement(config,'config'),'add',key='globalPackagesFolder',value=str(root/'tool-packages'))
ET.ElementTree(config).write(root/'Tool.NuGet.Config',encoding='unicode')
PY
release_version="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["version"])' "$repo_root/release/catalog.json")"
dotnet tool install EnterpriseWorkflow.Cli --version "$release_version" --tool-path "$qualification_root/tools" --configfile "$qualification_root/Tool.NuGet.Config"
cli_path="$qualification_root/tools/ew"
mkfifo "$qualification_root/dev.stdin"
exec 3<> "$qualification_root/dev.stdin"
trap cleanup EXIT
docker compose -f "$compose_path" up -d --wait
"$cli_path" pack "$qualification_root/LeaveRequest.Module" --configuration Release --output "$qualification_root/leave-artifacts"
"$cli_path" verify "$qualification_root/leave-artifacts/sample.leave-request/1.8.0"
"$cli_path" dev "$qualification_root/LeaveRequest.Module" --config "$qualification_root/ew.smtp.json" --configuration Release --no-watch --url "$base_url" < "$qualification_root/dev.stdin" > "$qualification_root/dev.log" 2>&1 &
runtime_pid=$!
for attempt in {1..60}; do
  if curl --fail --silent "$base_url/__ew/dev" >/dev/null; then break; fi
  if ! kill -0 "$runtime_pid" 2>/dev/null || [ "$attempt" -eq 60 ]; then cat "$qualification_root/dev.log"; exit 1; fi
  sleep 1
done
EW_DEV_TEST_URL="$base_url" EW_SMTP_TEST_URL="http://127.0.0.1:8025" EW_SMTP_COMPOSE_FILE="$compose_path" EW_DEV_TEST_DB="$qualification_root/LeaveRequest.Module/.ew/environments/default/workflow.db" \
  dotnet test --project "$repo_root/tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj" --configuration Release --no-build --no-restore -- --filter-method '*LeaveRequest*'

python3 "$repo_root/tests/cli/qualify-kernel.py" "$repo_root/src/EnterpriseWorkflow.Kernel/bin/Release/net10.0/EnterpriseWorkflow.Kernel.dll" "$qualification_root/leave-artifacts/sample.leave-request/1.8.0" "$qualification_root/kernel-qualification"
