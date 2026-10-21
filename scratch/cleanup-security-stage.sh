#!/usr/bin/env bash
set -euo pipefail
stage=/home/ubuntu/sentinellan-security-stage
test "$(readlink -f "$stage")" = /home/ubuntu/sentinellan-security-stage
test -d "$stage"
install -m 600 "$stage/security-hardening-2026-10-03.md" /opt/sentinellan/shared/security-hardening-2026-10-03.md
find "$stage" -maxdepth 1 -type f -delete
rmdir -- "$stage"
echo 'Temporary deployment helpers removed; recovery tools and encrypted backups retained.'
