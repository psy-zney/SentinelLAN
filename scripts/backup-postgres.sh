#!/usr/bin/env bash
set -euo pipefail
umask 077

COMPOSE_FILE="${1:-compose.yaml}"
ENV_FILE="${2:-.env}"
BACKUP_DIR="${SENTINELLAN_BACKUP_DIR:-backups/postgres}"

if [ ! -f "$COMPOSE_FILE" ] || [ ! -f "$ENV_FILE" ]; then
    echo "Compose file or environment file does not exist." >&2
    exit 1
fi
if ! docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
    sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"' >/dev/null; then
    echo "PostgreSQL container is unavailable." >&2
    exit 1
fi

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
target="$BACKUP_DIR/sentinellan-$stamp.dump"
temporary="$BACKUP_DIR/.sentinellan-$stamp-$$.tmp"
trap 'rm -f -- "$temporary"' EXIT

docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
    sh -c 'exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc --no-owner --no-privileges' > "$temporary"
test -s "$temporary"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
    pg_restore --list < "$temporary" > /dev/null
chmod 600 "$temporary"
mv -- "$temporary" "$target"
trap - EXIT
echo "Backup created: $target"
echo "Store this dump and the server vault key separately in encrypted, access-controlled storage."
