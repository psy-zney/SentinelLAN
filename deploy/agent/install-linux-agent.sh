#!/usr/bin/env bash
set -euo pipefail

# SentinelLAN Agent Installer for Linux VPS / Server Nodes
# Usage: sudo bash install-linux-agent.sh --server "http://your-vps-ip" --token "enrollment-token"

SERVER_URL=""
ENROLL_TOKEN=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --server) SERVER_URL="$2"; shift 2 ;;
        --token) ENROLL_TOKEN="$2"; shift 2 ;;
        *) echo "[!] Unknown option: $1"; exit 1 ;;
    esac
done

if [ -z "$SERVER_URL" ] || [ -z "$ENROLL_TOKEN" ]; then
    echo "Usage: sudo bash install-linux-agent.sh --server <SERVER_URL> --token <ENROLLMENT_TOKEN>"
    exit 1
fi

if [ "$EUID" -ne 0 ]; then
    echo "[!] Please run as root or with sudo."
    exit 1
fi

echo "[*] Installing SentinelLAN Agent..."

INSTALL_DIR="/opt/sentinellan-agent"
CONFIG_DIR="/etc/sentinellan"

mkdir -p "$INSTALL_DIR" "$CONFIG_DIR"

# Write environment configuration
cat <<EOF > "$CONFIG_DIR/agent.env"
SENTINELLAN_API_URL=${SERVER_URL}
SENTINELLAN_ENROLLMENT_TOKEN=${ENROLL_TOKEN}
SENTINELLAN_ALLOW_REAL_COMMANDS=true
SENTINELLAN_LAB_EXECUTION=true
EOF
chmod 600 "$CONFIG_DIR/agent.env"

# Copy binary if present locally
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -f "$SCRIPT_DIR/sentinellan-agent" ]; then
    cp "$SCRIPT_DIR/sentinellan-agent" "$INSTALL_DIR/sentinellan-agent"
elif [ -f "$SCRIPT_DIR/../../publish/linux-x64/sentinellan-agent" ]; then
    cp "$SCRIPT_DIR/../../publish/linux-x64/sentinellan-agent" "$INSTALL_DIR/sentinellan-agent"
fi

if [ ! -f "$INSTALL_DIR/sentinellan-agent" ]; then
    echo "[!] Agent binary not found in $INSTALL_DIR/sentinellan-agent."
    echo "    Please build it or copy the release binary to $INSTALL_DIR/sentinellan-agent"
    echo "    Command to build: dotnet publish apps/agent/src/SentinelLAN.Agent/SentinelLAN.Agent.csproj -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /opt/sentinellan-agent"
    exit 1
fi

chmod +x "$INSTALL_DIR/sentinellan-agent"

# Install and start systemd service
cp "$SCRIPT_DIR/sentinellan-agent.service" /etc/systemd/system/sentinellan-agent.service
systemctl daemon-reload
systemctl enable sentinellan-agent
systemctl restart sentinellan-agent

echo "[✓] SentinelLAN Agent installed and started as systemd service!"
systemctl status sentinellan-agent --no-pager
