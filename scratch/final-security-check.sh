#!/usr/bin/env bash
set -euo pipefail
cd /opt/sentinellan/current
for file in deploy/vps/compose-secure.sh deploy/vps/backup-encrypted.sh deploy/vps/check-internal-certificates.sh; do bash -n "$file"; done
bash deploy/vps/compose-secure.sh config --quiet
bash /home/ubuntu/sentinellan-security-stage/verify-encrypted.sh
python3 /home/ubuntu/sentinellan-security-stage/verify-production-auth.py
for timer in certbot.timer sentinellan-backup.timer sentinellan-certificate-check.timer; do
    test "$(systemctl is-active "$timer")" = active
done
count=$(docker ps --filter label=com.docker.compose.project=sentinellan-prod --format '{{.Names}}' | wc -l)
test "$count" = 9
test "$(docker inspect -f '{{.State.Health.Status}}' sentinellan-prod-postgres-1)" = healthy
test -z "$(find /opt/sentinellan/shared/backups -maxdepth 1 -name '*.dump' -print -quit)"
bash /opt/sentinellan/shared/check-internal-certificates.sh
echo 'Final operational checks passed.'
