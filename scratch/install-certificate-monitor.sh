#!/usr/bin/env bash
set -euo pipefail
install -m 750 /home/ubuntu/sentinellan-security-stage/check-internal-certificates.sh /opt/sentinellan/shared/check-internal-certificates.sh
install -m 750 /home/ubuntu/sentinellan-security-stage/check-internal-certificates.sh /opt/sentinellan/current/deploy/vps/check-internal-certificates.sh
cat > /etc/systemd/system/sentinellan-certificate-check.service <<'EOF'
[Unit]
Description=Check SentinelLAN internal TLS certificate expiry
[Service]
Type=oneshot
ExecStart=/opt/sentinellan/shared/check-internal-certificates.sh
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
PrivateTmp=true
EOF
cat > /etc/systemd/system/sentinellan-certificate-check.timer <<'EOF'
[Unit]
Description=Daily SentinelLAN internal certificate expiry check
[Timer]
OnCalendar=*-*-* 03:10:00 UTC
Persistent=true
[Install]
WantedBy=timers.target
EOF
systemctl daemon-reload
systemctl enable --now sentinellan-certificate-check.timer
systemctl start sentinellan-certificate-check.service
echo 'Daily internal TLS certificate expiry monitor active.'
