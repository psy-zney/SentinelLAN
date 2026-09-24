#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
    echo "Usage: $0 <BACKUP_FILE> [COMPOSE_FILE] [ENV_FILE]" >&2
    exit 1
fi
BACKUP_FILE="$1"
COMPOSE_FILE="${2:-compose.yaml}"
ENV_FILE="${3:-.env}"
if [ ! -s "$BACKUP_FILE" ] || [ ! -f "$COMPOSE_FILE" ] || [ ! -f "$ENV_FILE" ]; then
    echo "Backup, Compose file or environment file is missing." >&2
    exit 1
fi

drill_db="sentinellan_drill_$(date -u +%Y%m%d%H%M%S)_$$"
cleanup() {
    docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
        sh -c 'dropdb --if-exists -U "$POSTGRES_USER" "$1"' sh "$drill_db" >/dev/null || true
}
trap cleanup EXIT

docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
    sh -c 'createdb -U "$POSTGRES_USER" "$1"' sh "$drill_db"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
    sh -c 'pg_restore --exit-on-error --no-owner --no-privileges -U "$POSTGRES_USER" -d "$1"' sh "$drill_db" < "$BACKUP_FILE"
table_count="$(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T postgres \
    sh -c 'psql -U "$POSTGRES_USER" -d "$1" -Atqc "SELECT count(*) FROM information_schema.tables WHERE table_schema = '\''public'\''"' sh "$drill_db" | tr -d '\r')"
if [ -z "$table_count" ] || [ "$table_count" -lt 1 ]; then
    echo "Restore drill failed: no public tables were restored." >&2
    exit 1
fi
echo "Restore drill passed: $table_count public tables in an isolated temporary database."
