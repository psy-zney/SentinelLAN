#!/usr/bin/env bash
set -euo pipefail
: "${SENTINELLAN_DEMO_ADMIN_PASSWORD:?Set SENTINELLAN_DEMO_ADMIN_PASSWORD in this shell.}"
dotnet run --project apps/backend/src/SentinelLAN.Api
