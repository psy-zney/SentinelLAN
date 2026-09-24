#!/usr/bin/env bash
set -euo pipefail

# SentinelLAN Agent Installer for Linux VPS / Server Nodes
# Usage: sudo bash install-linux-agent.sh --server "https://example.com"

SERVER_URL=""
ENROLL_TOKEN=""
SIGNING_KEY_FILE=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --server) SERVER_URL="$2"; shift 2 ;;
        --token) ENROLL_TOKEN="$2"; shift 2 ;;
        --signing-key-file) SIGNING_KEY_FILE="$2"; shift 2 ;;
        *) echo "[!] Unknown option: $1"; exit 1 ;;
    esac
done

if [ -z "$SERVER_URL" ]; then
    echo "Usage: sudo bash install-linux-agent.sh --server <HTTPS_SERVER_URL> [--signing-key-file <ROOT_ONLY_FILE>]"
    exit 1
fi

if [ "$EUID" -ne 0 ]; then
    echo "[!] Please run as root or with sudo."
    exit 1
fi
if ! command -v openssl >/dev/null; then
    echo "openssl is required to create the local identity encryption key." >&2
    exit 1
fi
if [[ "$SERVER_URL" =~ [[:space:]] ]] || [[ "$ENROLL_TOKEN" =~ [[:space:]] ]]; then
    echo "Server URL and enrollment token must not contain whitespace." >&2
    exit 1
fi

if [[ "$SERVER_URL" != https://* ]] && [[ "$SERVER_URL" != http://localhost:* ]] && [[ "$SERVER_URL" != http://127.0.0.1:* ]]; then
    echo "A trusted HTTPS server URL is required outside loopback." >&2
    exit 1
fi
if [ -z "$ENROLL_TOKEN" ]; then
    read -r -s -p "One-time enrollment token: " ENROLL_TOKEN
    echo
fi
if [ -z "$ENROLL_TOKEN" ]; then
    echo "An enrollment token is required." >&2
    exit 1
fi
if [ -n "$SIGNING_KEY_FILE" ] && [ ! -r "$SIGNING_KEY_FILE" ]; then
    echo "Cannot read signing key file." >&2
    exit 1
fi

echo "[*] Installing SentinelLAN Agent..."

INSTALL_DIR="/opt/sentinellan-agent"
CONFIG_DIR="/etc/sentinellan"

mkdir -p "$INSTALL_DIR" "$CONFIG_DIR"
if ! id -u sentinellan >/dev/null 2>&1; then
    useradd --system --home-dir /var/lib/sentinellan-agent --shell /usr/sbin/nologin sentinellan
fi
install -d -m 700 -o sentinellan -g sentinellan /var/lib/sentinellan-agent
chmod 700 "$CONFIG_DIR"

# Write environment configuration
AGENT_STORE_KEY=""
if [ -f "$CONFIG_DIR/agent.env" ]; then
    AGENT_STORE_KEY="$(sed -n 's/^SENTINELLAN_AGENT_STORE_KEY=//p' "$CONFIG_DIR/agent.env" | tail -n 1)"
fi
if [ -z "$AGENT_STORE_KEY" ]; then AGENT_STORE_KEY="$(openssl rand -hex 32)"; fi
SIGNING_KEY=""
if [ -n "$SIGNING_KEY_FILE" ]; then SIGNING_KEY="$(cat "$SIGNING_KEY_FILE")"; fi
if [ -n "$SIGNING_KEY" ] && [[ ! "$SIGNING_KEY" =~ ^[0-9a-fA-F]{64}$ ]]; then
    echo "Signing key file must contain a 64-character hexadecimal key." >&2
    exit 1
fi
cat <<EOF > "$CONFIG_DIR/agent.env"
SENTINELLAN_API_URL=${SERVER_URL}
SENTINELLAN_ENROLLMENT_TOKEN=${ENROLL_TOKEN}
SENTINELLAN_AGENT_STORE_KEY=${AGENT_STORE_KEY}
SENTINELLAN_SIGNING_KEY=${SIGNING_KEY}
EOF
chmod 600 "$CONFIG_DIR/agent.env"
unset ENROLL_TOKEN AGENT_STORE_KEY SIGNING_KEY

# Copy binary if present locally
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -f "$SCRIPT_DIR/SentinelLAN.Agent" ]; then
    cp "$SCRIPT_DIR/SentinelLAN.Agent" "$INSTALL_DIR/sentinellan-agent"
elif [ -f "$SCRIPT_DIR/../../publish/linux-x64/SentinelLAN.Agent" ]; then
    cp "$SCRIPT_DIR/../../publish/linux-x64/SentinelLAN.Agent" "$INSTALL_DIR/sentinellan-agent"
fi

if [ ! -f "$INSTALL_DIR/sentinellan-agent" ]; then
    echo "[!] Agent binary not found in $INSTALL_DIR/sentinellan-agent."
    echo "    Please build it or copy the release binary to $INSTALL_DIR/sentinellan-agent"
    echo "    Command to build: dotnet publish apps/agent/src/SentinelLAN.Agent/SentinelLAN.Agent.csproj -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /opt/sentinellan-agent"
    exit 1
fi

chmod +x "$INSTALL_DIR/sentinellan-agent"
chown root:root "$INSTALL_DIR/sentinellan-agent"

# Install and start systemd service
cp "$SCRIPT_DIR/sentinellan-agent.service" /etc/systemd/system/sentinellan-agent.service
systemctl daemon-reload
systemctl enable sentinellan-agent
systemctl restart sentinellan-agent

echo "[✓] SentinelLAN Agent installed and started as systemd service!"
systemctl status sentinellan-agent --no-pager
