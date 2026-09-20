#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "${REQUIRE_POSTGRES:-0}" == "1" && -z "${ConnectionStrings__SentinelLAN:-}" ]]; then
  echo 'Set ConnectionStrings__SentinelLAN to an isolated PostgreSQL test database.' >&2
  exit 1
fi
dotnet restore SentinelLAN.slnx
dotnet format SentinelLAN.slnx --no-restore --verify-no-changes
dotnet build SentinelLAN.slnx --configuration Release --no-restore
dotnet test SentinelLAN.slnx --configuration Release --no-build --no-restore
npm run lint
npm run typecheck
npm test
npm run build
npm audit --audit-level=high
if [[ "${SKIP_BROWSER:-0}" != "1" ]]; then npm run test:e2e; fi
