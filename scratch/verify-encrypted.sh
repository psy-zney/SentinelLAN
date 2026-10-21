#!/usr/bin/env bash
set -euo pipefail
tls=/opt/sentinellan/shared/tls/20261003
domain=sentinellan.149.118.50.176.sslip.io
echo 'Database TLS sessions:'
docker exec sentinellan-prod-postgres-1 sh -c 'export PGPASSWORD="$POSTGRES_PASSWORD"; exec psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "SELECT ssl,version,bits,client_dn FROM pg_stat_ssl WHERE ssl"'
echo 'Published SentinelLAN ports:'
for service in api postgres company employee platform nginx; do
    docker inspect -f '{{.Name}} {{json .NetworkSettings.Ports}}' "sentinellan-prod-$service-1"
done
echo 'Public transport and response headers:'
curl -fsSI "https://$domain/platform/login" | head -20
session_headers=$(curl -sS -D - -o /dev/null "https://$domain/api/v1/auth/session")
echo "$session_headers"
[[ "$session_headers" = *'401 Unauthorized'* ]]
echo 'Gateway certificate verification:'
curl -fsS --cacert "$tls/gateway/ca.crt" https://127.0.0.1:3180/health/ready >/dev/null
echo 'TLS 1.2 compatibility check:'
if curl -fsS --tls-max 1.2 "https://$domain/health/ready" >/dev/null 2>&1; then
    echo 'The shared host Nginx default TLS listener also negotiates TLS 1.2.'
else
    echo 'TLS 1.2 did not serve the health endpoint.'
fi
echo 'Web loopback listeners:'
for service in company employee platform; do
    docker exec "sentinellan-prod-$service-1" node -e 'const net=require("node:net");let s=net.connect({host:"127.0.0.1",port:3000},()=>{console.log("loopback HTTP reachable");s.end();});s.on("error",()=>process.exit(1));'
done
echo 'Unrelated container continuity:'
while read -r name identifier; do
    case "$name" in sentinellan-prod-*) continue;; esac
    current=$(docker inspect -f '{{.Id}} {{.State.Running}}' "$name")
    [[ "$current" = "$identifier"*true ]]
    echo "$name retained"
done < /opt/sentinellan/shared/security-20261003/containers.before
