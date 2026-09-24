#!/usr/bin/env bash
set -euo pipefail
umask 077

# Requires a DNS name and a trusted certificate in ./certs/fullchain.pem and ./certs/privkey.pem.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [ "$EUID" -ne 0 ]; then
    echo "Run as root so the private configuration and certificate stay protected." >&2
    exit 1
fi

if ! command -v docker >/dev/null || ! docker compose version >/dev/null 2>&1; then
    echo "Install Docker Engine and the Compose plugin before running this script." >&2
    exit 1
fi
if ! command -v openssl >/dev/null || ! command -v curl >/dev/null; then
    echo "Install openssl and curl before running this script." >&2
    exit 1
fi
if [ ! -s certs/fullchain.pem ] || [ ! -s certs/privkey.pem ]; then
    echo "Place a trusted certificate and private key in deploy/vps/certs/ before deployment." >&2
    exit 1
fi

if [ ! -f .env ]; then
    : "${PUBLIC_DOMAIN:?Set PUBLIC_DOMAIN to a DNS name with a trusted certificate}"
    : "${BOOTSTRAP_ADMIN_EMAIL:?Set BOOTSTRAP_ADMIN_EMAIL to the initial administrator email}"
    : "${BOOTSTRAP_ORG_CODE:?Set BOOTSTRAP_ORG_CODE to the organization code}"
    : "${BOOTSTRAP_ORG_NAME:?Set BOOTSTRAP_ORG_NAME to the organization name}"
    if [[ ! "$PUBLIC_DOMAIN" =~ ^[a-zA-Z0-9.-]+$ ]] ||
       [[ ! "$BOOTSTRAP_ORG_CODE" =~ ^[a-z][a-z0-9-]{2,49}$ ]] ||
       [[ ! "$BOOTSTRAP_ADMIN_EMAIL" =~ ^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$ ]] ||
       (( ${#BOOTSTRAP_ORG_NAME} < 2 || ${#BOOTSTRAP_ORG_NAME} > 100 )) ||
       [[ "$BOOTSTRAP_ORG_NAME" == *$'\n'* ]] || [[ "$BOOTSTRAP_ORG_NAME" == *$'\r'* ]] ||
       [[ "$BOOTSTRAP_ORG_NAME" == *'$'* ]] || [[ "$BOOTSTRAP_ORG_NAME" == *'#'* ]] ||
       [[ "$BOOTSTRAP_ORG_NAME" == *'='* ]]; then
        echo "Bootstrap values contain characters unsafe for the Compose environment file." >&2
        exit 1
    fi
    cat > .env <<EOF
PUBLIC_DOMAIN=${PUBLIC_DOMAIN}
POSTGRES_DB=sentinellan
POSTGRES_USER=sentinellan
POSTGRES_PASSWORD=$(openssl rand -hex 32)
BOOTSTRAP_ORG_CODE=${BOOTSTRAP_ORG_CODE}
BOOTSTRAP_ORG_NAME=${BOOTSTRAP_ORG_NAME}
BOOTSTRAP_ADMIN_EMAIL=${BOOTSTRAP_ADMIN_EMAIL}
BOOTSTRAP_ADMIN_PASSWORD=$(openssl rand -hex 24)
SIGNING_KEY=$(openssl rand -hex 32)
ACCESS_TOKEN_SIGNING_KEY=$(openssl rand -hex 32)
SERVER_VAULT_KEY=$(openssl rand -hex 32)
EOF
    chmod 600 .env
    echo "Created deploy/vps/.env (mode 600). Read the bootstrap password from that file using a protected terminal."
else
    echo "Using existing deploy/vps/.env; secrets were not changed."
fi

DEPLOY_DOMAIN="$(sed -n 's/^PUBLIC_DOMAIN=//p' .env | tail -n 1)"
: "${DEPLOY_DOMAIN:?Set PUBLIC_DOMAIN in deploy/vps/.env}"

docker compose -f docker-compose.prod.yaml config --quiet
docker compose -f docker-compose.prod.yaml up -d --build

for attempt in $(seq 1 30); do
    if curl --silent --show-error --fail --resolve "${DEPLOY_DOMAIN}:443:127.0.0.1" \
        "https://${DEPLOY_DOMAIN}/health/ready" >/dev/null 2>&1; then
        echo "Ready: https://${DEPLOY_DOMAIN}"
        exit 0
    fi
    sleep 2
done

echo "Readiness failed. Inspect: docker compose -f docker-compose.prod.yaml logs api nginx" >&2
exit 1
