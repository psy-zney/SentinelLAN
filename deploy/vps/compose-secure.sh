#!/usr/bin/env bash
set -euo pipefail
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$root"
exec docker compose --env-file /opt/sentinellan/shared/.env \
  -f deploy/vps/docker-compose.prod.yaml \
  -f deploy/vps/docker-compose.host-proxy.yaml \
  -f deploy/vps/docker-compose.encrypted.yaml "$@"
