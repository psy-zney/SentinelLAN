#!/usr/bin/env bash
set -euo pipefail
umask 077
stage=/home/ubuntu/sentinellan-security-stage
shared=/opt/sentinellan/shared
old_release=$(readlink -f /opt/sentinellan/current)
release=/opt/sentinellan/releases/$(date -u +%Y%m%d-security-%H%M%S)
state=$shared/security-20261003
tls=$shared/tls/20261003
test ! -e "$state"
install -d -m 700 "$state" "$tls"
cp "$shared/.env" "$state/env.before"
cp /etc/nginx/sites-available/sentinellan.conf "$state/nginx.before"
cp "$shared/backup-daily.sh" "$state/backup.before"
docker ps --format '{{.Names}} {{.ID}}' > "$state/containers.before"
tar -xzf "$stage/server-material.tar.gz" -C "$tls"
rm -f -- "$stage/server-material.tar.gz"
find "$tls" -type d -exec chmod 750 {} +
find "$tls" -type f -exec chmod 600 {} +
find "$tls" -name '*.crt' -exec chmod 644 {} +
chown -R 70:70 "$tls/postgres"
chown -R root:1654 "$tls/api"
chmod 640 "$tls/api"/*.key
chmod 755 "$shared/tls"
# Nginx workers inherit established TLS contexts; private keys are read by the master only.
install -m 750 "$stage/backup-encrypted.sh" "$shared/backup-daily.sh"
bash "$shared/backup-daily.sh"
latest=$(find "$shared/backups" -maxdepth 1 -name '*.dump.cms' -printf '%f\n' | sort | tail -1)
test -n "$latest"
echo "$latest" > "$state/prechange-encrypted-backup"
install -d -m 755 "$release"
cp -a "$old_release/." "$release/"
for name in docker-compose.encrypted.yaml pg_hba.encrypted.conf nginx.web-tls.conf nginx.encrypted.conf nginx.host-encrypted.conf.template nginx.auth-limit.conf; do
    install -m 644 "$stage/$name" "$release/deploy/vps/$name"
done
printf '\nTLS_DIRECTORY=%s\n' "$tls" >> "$shared/.env"
cd "$release"
compose=(docker compose --env-file "$shared/.env" -f deploy/vps/docker-compose.prod.yaml -f deploy/vps/docker-compose.host-proxy.yaml -f deploy/vps/docker-compose.encrypted.yaml)
"${compose[@]}" config --quiet
domain=$(sed -n 's/^PUBLIC_DOMAIN=//p' "$shared/.env" | tail -1)
test "$domain" = sentinellan.149.118.50.176.sslip.io
sed -e "s/sentinel.example.com/$domain/g" -e "s|TLS_DIRECTORY|$tls|g" deploy/vps/nginx.host-encrypted.conf.template > "$state/nginx.after"
cat > "$state/rollback.sh" <<EOF
#!/usr/bin/env bash
set -euo pipefail
cp '$state/nginx.before' /etc/nginx/sites-available/sentinellan.conf
cp '$state/env.before' '$shared/.env'
rm -f /etc/nginx/conf.d/sentinellan-auth-limit.conf
docker rm -f sentinellan-prod-company-tls-1 sentinellan-prod-employee-tls-1 sentinellan-prod-platform-tls-1 2>/dev/null || true
cd '$old_release'
docker compose --env-file '$shared/.env' -f deploy/vps/docker-compose.prod.yaml -f deploy/vps/docker-compose.host-proxy.yaml up -d --no-build --remove-orphans
ln -sfn '$old_release' /opt/sentinellan/current
nginx -t
systemctl reload nginx
EOF
chmod 700 "$state/rollback.sh"
rollback() { echo 'Deployment failed; restoring the preceding configuration.' >&2; bash "$state/rollback.sh"; }
trap rollback ERR
# Stop the gateway while upstream namespaces are recreated; no database volumes are removed.
"${compose[@]}" stop nginx
"${compose[@]}" up -d --no-build postgres api company employee platform company-tls employee-tls platform-tls
"${compose[@]}" up -d --no-build nginx
for attempt in $(seq 1 45); do
    if curl -fsS --cacert "$tls/gateway/ca.crt" https://127.0.0.1:3180/health/ready >/dev/null; then break; fi
    sleep 2
done
curl -fsS --cacert "$tls/gateway/ca.crt" https://127.0.0.1:3180/health/ready >/dev/null
for page in company/login employee/login platform/login; do
    curl -fsS --cacert "$tls/gateway/ca.crt" -H "Host: $domain" "https://127.0.0.1:3180/$page" >/dev/null
done
install -m 644 deploy/vps/nginx.auth-limit.conf /etc/nginx/conf.d/sentinellan-auth-limit.conf
install -m 644 "$state/nginx.after" /etc/nginx/sites-available/sentinellan.conf
nginx -t
systemctl reload nginx
for page in health/ready company/login employee/login platform/login; do
    curl -fsS "https://$domain/$page" >/dev/null
done
ln -sfn "$release" /opt/sentinellan/current
trap - ERR
printf '%s\n' "$release" > "$state/release"
bash "$shared/backup-daily.sh"
echo "Encrypted deployment active: $release"
"${compose[@]}" ps --format json | python3 -c 'import sys,json; rows=[json.loads(s) for s in sys.stdin if s.strip()]; print("\n".join(x["Name"]+" "+x["State"] for x in rows))'
