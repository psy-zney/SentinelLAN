#!/usr/bin/env bash
# Reads an already decrypted, verified custom-format dump from stdin. No production mounts or network.
set -euo pipefail
name=sentinellan-backup-restore-drill
cleanup() { docker rm -f "$name" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if docker inspect "$name" >/dev/null 2>&1; then echo 'A restore drill is already running.' >&2; exit 1; fi
docker run -d --name "$name" --network none --read-only --user 70:70 \
    --cap-drop ALL --security-opt no-new-privileges:true --memory 768m --cpus 1 --pids-limit 128 \
    --tmpfs /var/lib/postgresql:rw,noexec,nosuid,uid=70,gid=70,size=384m \
    --tmpfs /var/run/postgresql:rw,noexec,nosuid,uid=70,gid=70,size=16m \
    --tmpfs /tmp:rw,noexec,nosuid,size=32m \
    -e POSTGRES_USER=restorecheck -e POSTGRES_DB=restorecheck -e POSTGRES_PASSWORD=fixture-only-not-a-real-secret \
    -e HOME=/tmp -e TMPDIR=/tmp \
    postgres:18.6-alpine@sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873 >/dev/null
for attempt in $(seq 1 30); do
    if docker exec "$name" pg_isready -U restorecheck -d restorecheck >/dev/null 2>&1; then break; fi
    sleep 1
done
docker exec -i "$name" pg_restore --exit-on-error --no-owner --no-privileges -U restorecheck -d restorecheck
tables=$(docker exec "$name" psql -U restorecheck -d restorecheck -Atc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'")
test "$tables" = 36
echo "Encrypted backup decrypted, signature verified, and restored: $tables public tables."
