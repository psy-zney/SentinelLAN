command -v python3 >/dev/null
command -v curl >/dev/null
command -v sha256sum >/dev/null
command -v openssl >/dev/null
if [ "$(id -u)" != 0 ]; then echo 'Run the reviewed bootstrap as root.' >&2; exit 1; fi
# Values are parsed as data; never eval JSON, shell source, URLs or request parameters.
read_cfg() { printf '%s' "$payload" | base64 --decode | python3 -c 'import json,sys; print(json.load(sys.stdin)[sys.argv[1]],end="")' "$1"; }
expiry=$(read_cfg ExpiresAt)
python3 -c 'from datetime import datetime,timezone; import sys; sys.exit(0 if datetime.now(timezone.utc)<datetime.fromisoformat(sys.argv[1]) else 1)' "$expiry" || { echo 'Enrollment package expired.' >&2; exit 1; }
if [ -e /var/lib/sentinellan-agent/identity.dat ]; then echo 'Agent is already enrolled; ask IT before replacing its identity.' >&2; exit 1; fi
download=$(mktemp)
trap 'rm -f -- "$download"; unset payload token store_key' EXIT
curl --fail --silent --show-error --proto '=https' --tlsv1.2 --max-time 120 --max-filesize 268435456 --output "$download" "$(read_cfg ArtifactUrl)"
printf '%s  %s\n' "$(read_cfg Sha256)" "$download" | sha256sum --check --status || { echo 'Release SHA-256 mismatch.' >&2; exit 1; }
# The Linux release is a single self-contained executable, not an untrusted archive.
install -d -m 755 /opt/sentinellan-agent
install -m 755 "$download" /opt/sentinellan-agent/sentinellan-agent
id sentinellan >/dev/null 2>&1 || useradd --system --home-dir /var/lib/sentinellan-agent --shell /usr/sbin/nologin sentinellan
install -d -m 700 -o sentinellan -g sentinellan /var/lib/sentinellan-agent
install -d -m 700 /etc/sentinellan
token=$(read_cfg Token)
store_key=$(openssl rand -hex 32)
{
  printf 'SENTINELLAN_API_URL=%s\n' "$(read_cfg ServerUrl)"
  printf 'SENTINELLAN_ENROLLMENT_TOKEN=%s\n' "$token"
  printf 'SENTINELLAN_COMMAND_PUBLIC_KEY=%s\n' "$(read_cfg PublicKey)"
  printf 'SENTINELLAN_COMMAND_KEY_ID=%s\n' "$(read_cfg KeyId)"
  printf 'SENTINELLAN_AGENT_STORE_KEY=%s\n' "$store_key"
} > /etc/sentinellan/agent.env
chmod 600 /etc/sentinellan/agent.env
cat > /etc/systemd/system/sentinellan-agent.service <<'UNIT'
[Unit]
Description=SentinelLAN authorized endpoint Agent
After=network-online.target
Wants=network-online.target
[Service]
User=sentinellan
Group=sentinellan
ExecStart=/opt/sentinellan-agent/sentinellan-agent
EnvironmentFile=/etc/sentinellan/agent.env
StateDirectory=sentinellan-agent
StateDirectoryMode=0700
Restart=always
RestartSec=10
ProtectSystem=strict
ProtectHome=true
NoNewPrivileges=true
PrivateTmp=true
ReadWritePaths=/var/lib/sentinellan-agent
CapabilityBoundingSet=
[Install]
WantedBy=multi-user.target
UNIT
systemctl daemon-reload
systemctl enable --now sentinellan-agent
connected=false
for attempt in $(seq 1 30); do
  if [ -s /var/lib/sentinellan-agent/identity.dat ]; then connected=true; break; fi
  sleep 2
done
# The one-time token is removed from disk regardless of success. Recovery needs a fresh IT package.
sed -i '/^SENTINELLAN_ENROLLMENT_TOKEN=/d' /etc/sentinellan/agent.env
if [ "$connected" != true ]; then echo 'Enrollment was not confirmed. Ask IT to inspect service status.' >&2; exit 1; fi
echo 'Agent identity has been saved. Check the device in the IT dashboard.'
