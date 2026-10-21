#!/usr/bin/env bash
set -euo pipefail
umask 077
release=/opt/sentinellan/releases/20261002-vps-monitor-220227
prior=$(readlink -f /opt/sentinellan/current)
test -f "$release/.vps-monitor-tests-passed"
cd "$release"
for app in api platform; do
  image=$(docker inspect --format '{{.Image}}' "sentinellan-prod-$app-1")
  docker image tag "$image" "sentinellan-prod-$app:before-20261002-vps-monitor-220227"
done
activation_started=0
rollback() {
  trap - ERR
  echo 'Deployment failed; restoring the prior API and platform images'
  for app in api platform; do docker image tag "sentinellan-prod-$app:before-20261002-vps-monitor-220227" "sentinellan-prod-$app:latest"; done
  if [ "$activation_started" = 1 ]; then
    cd "$prior"
    bash deploy/vps/compose-secure.sh up -d --no-deps api platform
    bash deploy/vps/compose-secure.sh up -d --no-deps --force-recreate platform-tls
    docker exec sentinellan-prod-nginx-1 nginx -s reload
    systemctl stop sentinellan-host-monitor.timer sentinellan-host-monitor.service || true
    systemctl disable sentinellan-host-monitor.timer || true
    ln -s "$prior" /opt/sentinellan/current.rollback
    mv -Tf /opt/sentinellan/current.rollback /opt/sentinellan/current
  fi
  exit 1
}
trap rollback ERR
docker inspect --format '{{.Id}}' sentinellan-prod-postgres-1 sentinellan-prod-company-1 sentinellan-prod-employee-1 \
  monopoly-game-server-1 livekit-livekit-1 mot-me-banh-api-1 exxplore-kittens-game-server-1 mot-me-banh-postgres-1 \
  > /opt/sentinellan/shared/vps-monitor-containers-before.txt
if ! bash deploy/vps/compose-secure.sh build api platform > /opt/sentinellan/shared/vps-monitor-build.log 2>&1; then
  tail -n 70 /opt/sentinellan/shared/vps-monitor-build.log
  rollback
fi
install -d -o root -g 1654 -m 750 /opt/sentinellan/shared/host-monitor
printf 'SENTINELLAN_HOST_NAME=vps-cong\nSENTINELLAN_HOST_ADDRESS=149.118.50.176\n' > /opt/sentinellan/shared/host-monitor.conf
chmod 600 /opt/sentinellan/shared/host-monitor.conf
install -o root -g root -m 644 deploy/vps/sentinellan-host-monitor.service /etc/systemd/system/sentinellan-host-monitor.service
install -o root -g root -m 644 deploy/vps/sentinellan-host-monitor.timer /etc/systemd/system/sentinellan-host-monitor.timer
SENTINELLAN_HOST_NAME=vps-cong SENTINELLAN_HOST_ADDRESS=149.118.50.176 python3 -B deploy/vps/collect-host-status.py
activation_started=1
bash deploy/vps/compose-secure.sh up -d --no-deps api platform
bash deploy/vps/compose-secure.sh up -d --no-deps --force-recreate platform-tls
docker exec sentinellan-prod-nginx-1 nginx -t
docker exec sentinellan-prod-nginx-1 nginx -s reload
for path in /platform/login /company/login /employee/login /health/ready; do
  curl --fail --retry 8 --retry-connrefused --retry-delay 2 --silent --output /dev/null "https://sentinellan.149.118.50.176.sslip.io$path"
  echo "PASS HTTPS $path"
done
docker inspect --format '{{.Id}}' sentinellan-prod-postgres-1 sentinellan-prod-company-1 sentinellan-prod-employee-1 \
  monopoly-game-server-1 livekit-livekit-1 mot-me-banh-api-1 exxplore-kittens-game-server-1 mot-me-banh-postgres-1 \
  > /opt/sentinellan/shared/vps-monitor-containers-after.txt
cmp /opt/sentinellan/shared/vps-monitor-containers-before.txt /opt/sentinellan/shared/vps-monitor-containers-after.txt
ln -s "$release" /opt/sentinellan/current.next
mv -Tf /opt/sentinellan/current.next /opt/sentinellan/current
systemctl daemon-reload
systemctl enable --now sentinellan-host-monitor.timer
systemctl start sentinellan-host-monitor.service
systemctl is-active --quiet sentinellan-host-monitor.timer
echo "ACTIVE_RELEASE=$release"
echo "Database, other web apps and unrelated Docker containers preserved"
