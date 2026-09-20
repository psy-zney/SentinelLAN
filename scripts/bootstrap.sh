#!/usr/bin/env bash
set -euo pipefail
for command in dotnet node npm; do command -v "$command" >/dev/null || { echo "Missing prerequisite: $command" >&2; exit 1; }; done
dotnet restore SentinelLAN.slnx
npm ci
echo 'Bootstrap complete. Copy .env.example to an untracked .env and set development secrets.'
