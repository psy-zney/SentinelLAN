#!/usr/bin/env bash
set -euo pipefail
cd /opt/sentinellan/current
install -o root -g root -m 644 /home/ubuntu/container-ports.tsx apps/platform/src/components/container-ports.tsx
install -o root -g root -m 644 /home/ubuntu/host-vps-monitor.tsx apps/platform/src/components/host-vps-monitor.tsx
install -o root -g root -m 644 /home/ubuntu/workspace.css apps/platform/src/app/workspace.css
docker image tag "$(docker inspect --format '{{.Image}}' sentinellan-prod-platform-1)" sentinellan-prod-platform:before-vps-monitor-mobile
if ! bash deploy/vps/compose-secure.sh build platform > /opt/sentinellan/shared/vps-monitor-platform-build.log 2>&1; then
  tail -n 50 /opt/sentinellan/shared/vps-monitor-platform-build.log
  docker image tag sentinellan-prod-platform:before-vps-monitor-mobile sentinellan-prod-platform:latest
  exit 1
fi
bash deploy/vps/compose-secure.sh up -d --no-deps platform
bash deploy/vps/compose-secure.sh up -d --no-deps --force-recreate platform-tls
docker exec sentinellan-prod-nginx-1 nginx -s reload
curl --fail --retry 8 --retry-connrefused --retry-delay 2 --silent --output /dev/null https://sentinellan.149.118.50.176.sslip.io/platform/login
echo 'Mobile VPS view deployed'
