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
npm run lint
npm run typecheck
npm test
npm run build
npm run lint:mobile
npm run typecheck:mobile
npm run test --workspace=@sentinellan/mobile -- --runInBand
(cd apps/mobile && npx expo export --platform android --output-dir ../../dist/mobile-android && npx expo export --platform ios --output-dir ../../dist/mobile-ios)
npm audit --omit=dev --audit-level=high
if [[ "${SKIP_BROWSER:-0}" != "1" ]]; then npm run test:e2e; fi
