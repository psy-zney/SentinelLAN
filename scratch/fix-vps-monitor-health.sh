#!/usr/bin/env bash
set -euo pipefail
cd /opt/sentinellan/current
install -o root -g root -m 644 /home/ubuntu/collect-host-status.py deploy/vps/collect-host-status.py
install -o root -g root -m 644 /home/ubuntu/VpsCommandCatalog.cs apps/backend/src/SentinelLAN.Infrastructure/VpsCommandCatalog.cs
python3 -B -m unittest discover -s deploy/vps/tests
systemctl start sentinellan-host-monitor.service
docker run --rm --name sentinellan-vps-monitor-tests --memory=3g --cpus=2 --pids-limit=256 \
  --mount "type=bind,src=$(pwd -P),dst=/source,readonly" --tmpfs /work:rw,size=2g \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 -e NUGET_PACKAGES=/work/nuget \
  mcr.microsoft.com/dotnet/sdk:10.0.400 bash -c \
  'set -e; cp /source/Directory.Build.props /source/Directory.Packages.props /source/global.json /work/; mkdir /work/apps; cp -a /source/apps/backend /source/apps/agent /work/apps/; cd /work; dotnet test apps/backend/tests/SentinelLAN.IntegrationTests/SentinelLAN.IntegrationTests.csproj --filter "FullyQualifiedName~Vps" --verbosity quiet'
docker image tag "$(docker inspect --format '{{.Image}}' sentinellan-prod-api-1)" sentinellan-prod-api:before-vps-monitor-health-fix
if ! bash deploy/vps/compose-secure.sh build api > /opt/sentinellan/shared/vps-monitor-api-build.log 2>&1; then
  tail -n 50 /opt/sentinellan/shared/vps-monitor-api-build.log
  docker image tag sentinellan-prod-api:before-vps-monitor-health-fix sentinellan-prod-api:latest
  exit 1
fi
bash deploy/vps/compose-secure.sh up -d --no-deps api
docker exec sentinellan-prod-nginx-1 nginx -s reload
curl --fail --retry 8 --retry-connrefused --retry-delay 2 --silent --output /dev/null https://sentinellan.149.118.50.176.sslip.io/health/ready
systemctl start sentinellan-host-monitor.service
echo 'VPS collector and SSH Docker inspection verified'
