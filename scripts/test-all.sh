#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "${REQUIRE_POSTGRES:-0}" == "1" && -z "${ConnectionStrings__SentinelLAN:-}" ]]; then
  echo 'Set ConnectionStrings__SentinelLAN to an isolated PostgreSQL test database.' >&2
  exit 1
fi
dotnet restore SentinelLAN.slnx
dotnet build SentinelLAN.slnx --configuration Release --no-restore
dotnet test SentinelLAN.slnx --configuration Release --no-build --no-restore
python3 -B -m unittest discover -s deploy/vps/tests -v
npm run audit:dependencies:all
npm run lint
npm run typecheck
npm run test:coverage
npm run build
npm run check:sdk --workspace=@sentinellan/mobile
npm run lint:mobile
npm run typecheck:mobile
npm run test:coverage --workspace=@sentinellan/mobile -- --runInBand
if [[ "${SKIP_BROWSER:-0}" != "1" ]]; then
  ConnectionStrings__SentinelLAN='' \
    SENTINELLAN_E2E_API_COMMAND="${SENTINELLAN_E2E_API_COMMAND:-dotnet ../backend/src/SentinelLAN.Api/bin/Release/net10.0/SentinelLAN.Api.dll}" \
    npm run test:e2e
fi
