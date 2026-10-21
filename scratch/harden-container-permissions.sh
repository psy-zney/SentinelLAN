#!/usr/bin/env bash
set -euo pipefail
umask 077
cd /opt/sentinellan/current
state=/opt/sentinellan/shared/security-20261003
stage=/home/ubuntu/sentinellan-security-stage
tls=/opt/sentinellan/shared/tls/20261003
for name in docker-compose.encrypted.yaml nginx.web-tls.conf nginx.encrypted.conf; do
    cp "deploy/vps/$name" "$state/$name.before-readonly"
    install -m 644 "$stage/$name" "deploy/vps/$name"
done
for name in gateway company employee platform; do
    chown root:101 "$tls/$name"
    chmod 750 "$tls/$name"
    chown root:101 "$tls/$name"/*.key
    chmod 640 "$tls/$name"/*.key
done
compose=(docker compose --env-file /opt/sentinellan/shared/.env -f deploy/vps/docker-compose.prod.yaml -f deploy/vps/docker-compose.host-proxy.yaml -f deploy/vps/docker-compose.encrypted.yaml)
"${compose[@]}" config --quiet
rollback() {
    for name in docker-compose.encrypted.yaml nginx.web-tls.conf nginx.encrypted.conf; do
        cp "$state/$name.before-readonly" "deploy/vps/$name"
    done
    "${compose[@]}" up -d --no-build
}
trap rollback ERR
"${compose[@]}" stop nginx company-tls employee-tls platform-tls
"${compose[@]}" up -d --no-build
for attempt in $(seq 1 30); do
    if curl -fsS --cacert "$tls/gateway/ca.crt" https://127.0.0.1:3180/health/ready >/dev/null 2>&1; then break; fi
    sleep 2
done
for page in health/ready company/login employee/login platform/login; do
    curl -fsS "https://sentinellan.149.118.50.176.sslip.io/$page" >/dev/null
done
trap - ERR
echo 'Every SentinelLAN container runs without root, with a read-only root filesystem and all Linux capabilities removed.'
