#!/usr/bin/env bash
set -euo pipefail
release=/opt/sentinellan/releases/20261003-ui-003148
test -d "$release"
for url_path in /platform/login /company/login /employee/login /health/ready; do
  code=$(curl --fail --retry 5 --retry-connrefused --retry-delay 2 --silent --output /dev/null --write-out '%{http_code}' "https://sentinellan.149.118.50.176.sslip.io$url_path")
  test "$code" = 200
  echo "PASS $url_path $code"
done
umask 077
docker inspect --format '{{.Id}}' sentinellan-prod-api-1 sentinellan-prod-postgres-1 > /opt/sentinellan/shared/ui-backend-after.txt
cmp /opt/sentinellan/shared/ui-backend-before.txt /opt/sentinellan/shared/ui-backend-after.txt
ln -s "$release" /opt/sentinellan/current.next
mv -Tf /opt/sentinellan/current.next /opt/sentinellan/current
echo "ACTIVE_RELEASE=$release"
echo 'API and database container identities verified'
docker ps --filter name=sentinellan-prod --format '{{.Names}} {{.Status}}'
