#!/usr/bin/env bash
set -euo pipefail
umask 077

# The host HTTPS site is configured separately. All application ports stay on loopback.
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
if [ ! -f .env ]; then
    : "${PUBLIC_DOMAIN:?Set PUBLIC_DOMAIN to a DNS name with a trusted certificate}"
    : "${TLS_DIRECTORY:?Set TLS_DIRECTORY to the provisioned internal TLS directory}"
    HOST_PROXY_PORT=${HOST_PROXY_PORT:-9003}
    if [[ ! "$HOST_PROXY_PORT" =~ ^[0-9]+$ ]] || (( HOST_PROXY_PORT <= 9000 || HOST_PROXY_PORT > 65535 )) ||
       [[ ! "$TLS_DIRECTORY" =~ ^/[A-Za-z0-9/_-]+$ ]]; then
        echo "Use a loopback application port above 9000 and a safe absolute TLS directory." >&2
        exit 1
    fi
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
    command_private_key=$(openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 2>/dev/null)
    cat > .env <<EOF
PUBLIC_DOMAIN=${PUBLIC_DOMAIN}
HOST_PROXY_PORT=${HOST_PROXY_PORT}
TLS_DIRECTORY=${TLS_DIRECTORY}
POSTGRES_DB=sentinellan
POSTGRES_USER=sentinellan
POSTGRES_PASSWORD=$(openssl rand -hex 32)
BOOTSTRAP_ORG_CODE=${BOOTSTRAP_ORG_CODE}
BOOTSTRAP_ORG_NAME=${BOOTSTRAP_ORG_NAME}
BOOTSTRAP_ADMIN_EMAIL=${BOOTSTRAP_ADMIN_EMAIL}
BOOTSTRAP_ADMIN_PASSWORD=$(openssl rand -hex 24)
COMMAND_PRIVATE_KEY_PEM='${command_private_key}'
COMMAND_KEY_ID=company-$(date -u +%Y%m%d)
ACCESS_TOKEN_SIGNING_KEY=$(openssl rand -hex 32)
SERVER_VAULT_KEY=$(openssl rand -hex 32)
SENTINELLAN_DATA_ENCRYPTION_KEY=$(openssl rand -base64 32)
EOF
    chmod 600 .env
    echo "Created deploy/vps/.env (mode 600). Read the bootstrap password from that file using a protected terminal."
else
    echo "Using existing deploy/vps/.env; secrets were not changed."
fi

DEPLOY_DOMAIN="$(sed -n 's/^PUBLIC_DOMAIN=//p' .env | tail -n 1)"
: "${DEPLOY_DOMAIN:?Set PUBLIC_DOMAIN in deploy/vps/.env}"
DEPLOY_TLS_DIRECTORY="$(sed -n 's/^TLS_DIRECTORY=//p' .env | tail -n 1)"
DEPLOY_PROXY_PORT="$(sed -n 's/^HOST_PROXY_PORT=//p' .env | tail -n 1)"
: "${DEPLOY_TLS_DIRECTORY:?Set TLS_DIRECTORY in deploy/vps/.env}"
: "${DEPLOY_PROXY_PORT:?Set HOST_PROXY_PORT in deploy/vps/.env}"
if [[ ! "$DEPLOY_PROXY_PORT" =~ ^[0-9]+$ ]] || (( DEPLOY_PROXY_PORT <= 9000 || DEPLOY_PROXY_PORT > 65535 )); then
    echo "The gateway port must be above 9000 on this shared VPS." >&2
    exit 1
fi

docker compose -f docker-compose.prod.yaml -f docker-compose.host-proxy.yaml -f docker-compose.encrypted.yaml config --quiet
docker compose -f docker-compose.prod.yaml -f docker-compose.host-proxy.yaml -f docker-compose.encrypted.yaml up -d --build --remove-orphans

for attempt in $(seq 1 30); do
    if curl --silent --show-error --fail --cacert "$DEPLOY_TLS_DIRECTORY/gateway/ca.crt" \
        --resolve "gateway:${DEPLOY_PROXY_PORT}:127.0.0.1" "https://gateway:${DEPLOY_PROXY_PORT}/health/ready" >/dev/null 2>&1; then
        echo "Origin health ready. Configure the host Nginx site and Cloudflare DNS for https://${DEPLOY_DOMAIN}."
        exit 0
    fi
    sleep 2
done

echo "Readiness failed. Inspect: docker compose -f docker-compose.prod.yaml logs api nginx" >&2
exit 1
