#!/usr/bin/env bash
set -euo pipefail

# SentinelLAN VPS One-Click Production Deployment Script
# Target OS: Ubuntu 22.04 / 24.04 LTS, Debian 12

echo "============================================================"
echo "    SentinelLAN — Production VPS Deployment Setup           "
echo "============================================================"

if [ "$EUID" -ne 0 ]; then
  echo "[!] Please run this script with sudo or as root."
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 1. Check and install Docker if missing
if ! command -v docker &> /dev/null; then
    echo "[*] Docker not found. Installing Docker Engine..."
    apt-get update
    apt-get install -y ca-certificates curl gnupg lsb-release
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    chmod a+r /etc/apt/keyrings/docker.gpg
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null
    apt-get update
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    systemctl enable docker
    systemctl start docker
    echo "[✓] Docker installed successfully."
fi

# 2. Generate secure production .env if not exists
if [ ! -f .env ]; then
    echo "[*] Generating cryptographically secure .env configuration..."
    DB_PASS=$(openssl rand -hex 24)
    ADM_PASS=$(openssl rand -base64 16 | tr -dc 'a-zA-Z0-9!@#$%')
    SIGN_KEY=$(openssl rand -hex 32)
    JWT_KEY=$(openssl rand -hex 32)
    ENROLL_TOK=$(openssl rand -hex 16)
    VPS_IP=$(curl -s -4 ifconfig.me || hostname -I | awk '{print $1}')

    cat <<EOF > .env
POSTGRES_DB=sentinellan
POSTGRES_USER=sentinellan
POSTGRES_PASSWORD=${DB_PASS}
ADMIN_EMAIL=admin@sentinellan.local
ADMIN_PASSWORD=${ADM_PASS}
SIGNING_KEY=${SIGN_KEY}
ACCESS_TOKEN_SIGNING_KEY=${JWT_KEY}
ENROLLMENT_TOKEN=${ENROLL_TOK}
WEB_ORIGINS=http://${VPS_IP},http://localhost
NEXT_PUBLIC_API_URL=
EOF
    echo "[✓] Configuration written to deploy/vps/.env"
else
    echo "[*] Existing .env file found. Preserving current secrets."
fi

# Load variables
set -a
# shellcheck source=/dev/null
source .env
set +a

# 3. Build and launch services
echo "[*] Building and starting SentinelLAN containers..."
docker compose -f docker-compose.prod.yaml down --remove-orphans || true
docker compose -f docker-compose.prod.yaml up -d --build

# 4. Wait for readiness
echo "[*] Waiting for API readiness check..."
ATTEMPTS=0
MAX_ATTEMPTS=30
while [ $ATTEMPTS -lt $MAX_ATTEMPTS ]; do
    if curl -s -f http://localhost/health/ready > /dev/null 2>&1; then
        echo "[✓] SentinelLAN Production API is LIVE and READY!"
        break
    fi
    ATTEMPTS=$((ATTEMPTS + 1))
    sleep 2
done

if [ $ATTEMPTS -eq $MAX_ATTEMPTS ]; then
    echo "[!] Warning: Readiness check timed out. Inspect logs with: docker compose -f docker-compose.prod.yaml logs"
fi

VPS_HOST="${WEB_ORIGINS%%,*}"
echo ""
echo "============================================================"
echo "    Deployment Complete! Keep this information safe:       "
echo "============================================================"
echo " Web Dashboard URL   : ${VPS_HOST}"
echo " Admin Email         : ${ADMIN_EMAIL}"
echo " Admin Password      : ${ADMIN_PASSWORD}"
echo " Agent Enroll Token  : ${ENROLLMENT_TOKEN}"
echo "============================================================"
echo ""
echo "To enroll a Windows PC or another Linux VPS:"
echo "  Windows (PowerShell as Admin):"
echo "    .\\deploy\\agent\\install-windows-agent.ps1 -ServerUrl \"${VPS_HOST}\" -EnrollToken \"${ENROLLMENT_TOKEN}\""
echo ""
echo "  Linux (Bash as Root):"
echo "    sudo bash ./deploy/agent/install-linux-agent.sh --server \"${VPS_HOST}\" --token \"${ENROLLMENT_TOKEN}\""
echo "============================================================"
