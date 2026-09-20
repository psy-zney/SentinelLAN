#!/usr/bin/env bash
set -euo pipefail
command -v docker >/dev/null || { echo 'Docker Compose v2 is required.' >&2; exit 1; }
docker compose up --build
