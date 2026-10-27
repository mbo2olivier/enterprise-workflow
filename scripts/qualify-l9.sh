#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
if command -v cygpath >/dev/null 2>&1; then repo_root="$(cygpath -m "$repo_root")"; fi
qualification_root="${1:?Usage: qualify-l9.sh <new-output-directory>}"
source_mode="${2:-local}"
python_bin="$(command -v python3 || command -v python)"
release_version="$("$python_bin" -c 'import json,sys; print(json.load(open(sys.argv[1]))["version"])' "$repo_root/release/catalog.json")"
if [ "$source_mode" != local ] && [ "$source_mode" != public ]; then
  echo "Source mode must be local or public." >&2
  exit 1
fi
if [ -e "$qualification_root" ]; then
  echo "Choose a new qualification directory; existing data will not be overwritten." >&2
  exit 1
fi
mkdir -p "$qualification_root"
qualification_root="$(cd "$qualification_root" && pwd)"
if command -v cygpath >/dev/null 2>&1; then qualification_root="$(cygpath -m "$qualification_root")"; fi
cp "$repo_root/global.json" "$qualification_root/global.json"
if [ "$source_mode" = local ]; then
  if [ -n "${EW_QUALIFICATION_FEED:-}" ]; then
    feed_source="$EW_QUALIFICATION_FEED"
    if command -v cygpath >/dev/null 2>&1; then feed_source="$(cygpath -m "$feed_source")"; fi
    mkdir -p "$qualification_root/feed"
    cp "$feed_source"/*.nupkg "$qualification_root/feed/"
  else
  "$repo_root/scripts/pack-local.sh" "$qualification_root/feed"
  fi
cat > "$qualification_root/NuGet.Config" <<CONFIG
<configuration><packageSources><clear /><add key="local" value="$qualification_root/feed" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources><packageSourceMapping><packageSource key="local"><package pattern="EnterpriseWorkflow.*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping><config><add key="globalPackagesFolder" value="$qualification_root/packages" /></config></configuration>
CONFIG
else
cat > "$qualification_root/NuGet.Config" <<CONFIG
<configuration><packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources><config><add key="globalPackagesFolder" value="$qualification_root/packages" /></config></configuration>
CONFIG
fi
# Keep package extraction and template registration isolated from developer installations.
export NUGET_PACKAGES="$qualification_root/packages"
if [ "$source_mode" = local ]; then
cat > "$qualification_root/Tool.NuGet.Config" <<CONFIG
<configuration><packageSources><clear /><add key="local" value="$qualification_root/feed" /></packageSources></configuration>
CONFIG
else
cp "$qualification_root/NuGet.Config" "$qualification_root/Tool.NuGet.Config"
fi
dotnet tool install EnterpriseWorkflow.Cli --version "$release_version" --tool-path "$qualification_root/tools" --configfile "$qualification_root/Tool.NuGet.Config"
if [ "$source_mode" = local ]; then
dotnet new install "$qualification_root/feed/EnterpriseWorkflow.Templates.$release_version.nupkg" --debug:custom-hive "$qualification_root/templates"
else
  # Template installation uses the isolated NuGet config from this working directory.
  (cd "$qualification_root" && dotnet new install "EnterpriseWorkflow.Templates::$release_version" --nuget-source https://api.nuget.org/v3/index.json --debug:custom-hive "$qualification_root/templates")
fi
dotnet new ew-module --name Acme.Leave --module-id acme.leave --output "$qualification_root/Acme.Leave" --debug:custom-hive "$qualification_root/templates" --no-update-check
dotnet new ew-embedded --name Acme.Portal --output "$qualification_root/Acme.Portal" --debug:custom-hive "$qualification_root/templates" --no-update-check
cli_path="$qualification_root/tools/ew"
if [ -f "$cli_path.exe" ]; then cli_path="$cli_path.exe"; fi
"$cli_path" validate "$qualification_root/Acme.Leave"
"$cli_path" inspect "$qualification_root/Acme.Leave" --format json --output "$qualification_root/resolved-module.json"
"$cli_path" pack "$qualification_root/Acme.Leave" --output "$qualification_root/artifacts"
"$cli_path" verify "$qualification_root/artifacts/acme.leave/1.0.0"
dotnet build "$qualification_root/Acme.Portal" -p:UseSharedCompilation=false -m:1
cd "$qualification_root"
dotnet run --project Acme.Portal --no-build -- "$qualification_root/artifacts/acme.leave/1.0.0" acme.leave.request 1 '{"reason":"Qualification L9"}' acme.leave.request acme.leave.read

python_bin="$(command -v python3 || command -v python)"
"$python_bin" "$repo_root/tests/cli/qualify-dev.py" "$cli_path" "$qualification_root/Acme.Leave"
