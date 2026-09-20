$ErrorActionPreference = 'Stop'
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker Compose v2 is required for the full development stack.' }
docker compose up --build
