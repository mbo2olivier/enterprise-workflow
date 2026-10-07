#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_dir="$(cd "${script_dir}/../.." && pwd)"
environment_file="${script_dir}/.env"
compose_files=(
  --env-file "${environment_file}"
  -f "${script_dir}/docker-compose.yml"
  -f "${script_dir}/docker-compose.19c.yml"
)

usage() {
  echo "Usage: $0 init|rotate|up|reset|test|status|down|destroy [arguments dotnet test]" >&2
  exit 2
}

write_environment() {
  umask 077
  local generated_password
  generated_password="Ew9$(openssl rand -hex 12)"
  printf 'ORACLE_TEST_PASSWORD=%s\nORACLE_TEST_PORT=1521\n' "${generated_password}" > "${environment_file}"
  chmod 600 "${environment_file}"
  echo "Configuration Oracle locale créée dans ${environment_file}."
}

read_setting() {
  local name="$1"
  awk -F= -v key="${name}" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "${environment_file}"
}

initialize_environment() {
  if [[ -e "${environment_file}" ]]; then
    local existing_password
    existing_password="$(read_setting ORACLE_TEST_PASSWORD)"
    if [[ "${existing_password}" =~ ^[A-Za-z][A-Za-z0-9_]{11,29}$ ]]; then
      chmod 600 "${environment_file}"
      echo "${environment_file} contient déjà une configuration locale ; aucun secret n'a été remplacé." >&2
      return
    fi
    echo "Le secret absent, factice ou incompatible de ${environment_file} est remplacé par un secret Oracle local." >&2
  fi
  write_environment
}

require_environment() {
  if [[ ! -f "${environment_file}" ]]; then
    echo "${environment_file} est absent. Exécutez d'abord : $0 init" >&2
    exit 1
  fi
  chmod 600 "${environment_file}"
  oracle_password="$(read_setting ORACLE_TEST_PASSWORD)"
  oracle_port="$(read_setting ORACLE_TEST_PORT)"
  oracle_port="${oracle_port:-1521}"
  if [[ ! "${oracle_password}" =~ ^[A-Za-z][A-Za-z0-9_]{11,29}$ ]]; then
    echo "ORACLE_TEST_PASSWORD doit contenir 12 à 30 lettres, chiffres ou underscores et commencer par une lettre." >&2
    exit 1
  fi
  if [[ ! "${oracle_port}" =~ ^[0-9]{1,5}$ ]] || (( oracle_port < 1 || oracle_port > 65535 )); then
    echo "ORACLE_TEST_PORT doit être un port TCP valide." >&2
    exit 1
  fi
}

compose() {
  docker compose "${compose_files[@]}" "$@"
}

action="${1:-}"
[[ -n "${action}" ]] || usage
shift

case "${action}" in
  init)
    initialize_environment
    ;;
  rotate)
    write_environment
    echo "Le secret a changé ; exécutez '$0 reset' avant toute connexion." >&2
    ;;
  up)
    require_environment
    compose up -d --wait
    ;;
  reset)
    require_environment
    compose down --volumes --remove-orphans
    compose up -d --wait
    ;;
  test)
    require_environment
    compose up -d --wait
    connection_string="User Id=EWTEST;Password=${oracle_password};Data Source=localhost:${oracle_port}/ORCLPDB1;Connection Timeout=30"
    cd "${repository_dir}"
    ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING="${connection_string}" \
      dotnet test --project tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj \
      --configuration Release "$@"
    ;;
  status)
    require_environment
    compose ps
    ;;
  down)
    require_environment
    compose down --remove-orphans
    ;;
  destroy)
    require_environment
    compose down --volumes --remove-orphans
    ;;
  *)
    usage
    ;;
esac
