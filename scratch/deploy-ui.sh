#!/usr/bin/env bash
set -euo pipefail
release=/opt/sentinellan/releases/20261003-ui-003810
prior=$(readlink -f /opt/sentinellan/current)
archive=/home/ubuntu/sentinellan-ui.tar.gz
test -d "$prior"
test ! -e "$release"
mkdir -m 755 "$release"
cp -a "$prior/." "$release/"
tar -xzf "$archive" -C "$release"
cd "$release"
umask 077
for app in company employee platform; do
  container="sentinellan-prod-$app-1"
  image_id=$(docker inspect --format '{{.Image}}' "$container")
  docker image tag "$image_id" "sentinellan-prod-$app:before-20261003-ui-003810"
done
docker inspect --format '{{.Id}}' sentinellan-prod-api-1 sentinellan-prod-postgres-1 > /opt/sentinellan/shared/ui-backend-before.txt
echo 'Building platform layout refinement'
if ! docker compose -f deploy/vps/docker-compose.prod.yaml -f deploy/vps/docker-compose.host-proxy.yaml build platform > /opt/sentinellan/shared/ui-build.log 2>&1; then
  tail -n 70 /opt/sentinellan/shared/ui-build.log
  exit 1
fi
echo 'Platform production build passed'
docker compose -f deploy/vps/docker-compose.prod.yaml -f deploy/vps/docker-compose.host-proxy.yaml up -d --no-deps platform
docker exec sentinellan-prod-nginx-1 nginx -t
docker exec sentinellan-prod-nginx-1 nginx -s reload
for url_path in /platform/login /company/login /employee/login /health/ready; do
  code=$(curl --fail --retry 5 --retry-connrefused --retry-delay 2 --silent --output /dev/null --write-out '%{http_code}' "https://sentinellan.149.118.50.176.sslip.io$url_path")
  test "$code" = 200
  echo "PASS $url_path $code"
done
docker inspect --format '{{.Id}}' sentinellan-prod-api-1 sentinellan-prod-postgres-1 > /opt/sentinellan/shared/ui-backend-after.txt
cmp /opt/sentinellan/shared/ui-backend-before.txt /opt/sentinellan/shared/ui-backend-after.txt
ln -s "$release" /opt/sentinellan/current.next
mv -Tf /opt/sentinellan/current.next /opt/sentinellan/current
echo "ACTIVE_RELEASE=$release"
echo 'API and database container identities verified'
