#!/usr/bin/env bash
set -euo pipefail
umask 077
file=/etc/ssh/sshd_config.d/00-sentinellan-hardening.conf
test ! -e "$file"
cat > "$file" <<'EOF'
PermitRootLogin no
PubkeyAuthentication yes
PasswordAuthentication no
KbdInteractiveAuthentication no
ChallengeResponseAuthentication no
MaxAuthTries 3
LoginGraceTime 30
X11Forwarding no
EOF
if ! sshd -t; then rm -f -- "$file"; exit 1; fi
systemctl reload ssh
sshd -T | awk '/^(permitrootlogin|pubkeyauthentication|passwordauthentication|kbdinteractiveauthentication|maxauthtries|logingracetime|x11forwarding) /'
