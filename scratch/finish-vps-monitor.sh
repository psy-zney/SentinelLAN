#!/usr/bin/env bash
set -euo pipefail
install -o root -g root -m 600 /home/ubuntu/vps-monitoring.md /opt/sentinellan/shared/vps-monitoring.md
install -o root -g root -m 600 /home/ubuntu/vps-monitor-verification-2026-10-03.md /opt/sentinellan/shared/vps-monitor-verification-2026-10-03.md
install -d -o root -g root -m 755 /opt/sentinellan/current/docs/adr
install -o root -g root -m 644 /home/ubuntu/0025-host-vps-snapshot.md /opt/sentinellan/current/docs/adr/0025-host-vps-snapshot.md
rm -f -- /home/ubuntu/read-ui-accounts.py /home/ubuntu/verify-vps-monitor.py \
  /home/ubuntu/verify-monitor-container-permissions.py /home/ubuntu/debug-host-inspect.py \
  /home/ubuntu/prepare-vps-monitor.sh /home/ubuntu/deploy-vps-monitor.sh \
  /home/ubuntu/fix-vps-monitor-health.sh /home/ubuntu/refine-vps-monitor-ui.sh \
  /home/ubuntu/collect-host-status.py /home/ubuntu/VpsCommandCatalog.cs \
  /home/ubuntu/container-ports.tsx /home/ubuntu/host-vps-monitor.tsx /home/ubuntu/workspace.css \
  /home/ubuntu/sentinellan-vps-monitor.tar.gz /home/ubuntu/vps-monitoring.md \
  /home/ubuntu/vps-monitor-verification-2026-10-03.md /home/ubuntu/0025-host-vps-snapshot.md
echo 'Verification report installed; temporary deployment and sign-in helpers removed'
rm -f -- /home/ubuntu/finish-vps-monitor.sh
