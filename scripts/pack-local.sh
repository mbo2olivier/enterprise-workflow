#!/usr/bin/env bash
set -euo pipefail
# Local artifacts only. Public publication belongs to L10.
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
if command -v cygpath >/dev/null 2>&1; then repo_root="$(cygpath -m "$repo_root")"; fi
feed_path="${1:-$repo_root/artifacts/nuget}"
mkdir -p "$feed_path"
feed_path="$(cd "$feed_path" && pwd)"
if command -v cygpath >/dev/null 2>&1; then feed_path="$(cygpath -m "$feed_path")"; fi
for project in "$repo_root"/src/*/*.csproj; do
  case "$project" in
    */EnterpriseWorkflow.Kernel/*|*/EnterpriseWorkflow.Cli/*) continue ;;
  esac
  dotnet pack "$project" --configuration Release --output "$feed_path" -p:UseSharedCompilation=false -m:1
done
dotnet pack "$repo_root/src/EnterpriseWorkflow.Cli/EnterpriseWorkflow.Cli.csproj" --configuration Release --output "$feed_path" -p:UseSharedCompilation=false -m:1
dotnet pack "$repo_root/templates/EnterpriseWorkflow.Templates.csproj" --configuration Release --output "$feed_path" -p:UseSharedCompilation=false -m:1
