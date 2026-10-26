#!/usr/bin/env bash
set -euo pipefail
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$root"
env_file=${SENTINELLAN_ENV_FILE:-/opt/sentinellan/shared/.env}
if [[ ! -r "$env_file" ]]; then
  echo "SentinelLAN environment file is missing or unreadable: $env_file" >&2
  exit 1
fi
export SENTINELLAN_SOURCE_REVISION
SENTINELLAN_SOURCE_REVISION=$(git rev-parse HEAD)
exec docker compose --env-file "$env_file" \
  -f deploy/vps/docker-compose.prod.yaml \
  -f deploy/vps/docker-compose.host-proxy.yaml \
  -f deploy/vps/docker-compose.encrypted.yaml "$@"
