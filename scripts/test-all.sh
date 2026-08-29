#!/usr/bin/env bash
set -euo pipefail
dotnet restore SentinelLAN.slnx
dotnet format SentinelLAN.slnx --verify-no-changes --no-restore
dotnet build SentinelLAN.slnx --configuration Release --no-restore
dotnet test SentinelLAN.slnx --configuration Release --no-build
npm ci
npm run lint
npm run typecheck
npm test
npm run build
if command -v docker >/dev/null; then docker compose config --quiet; else echo 'Docker unavailable; Compose verification skipped.' >&2; fi
if command -v npx >/dev/null; then npm run test:e2e; else echo 'npx unavailable; browser E2E skipped.' >&2; fi
