#!/usr/bin/env bash
set -euo pipefail
backup_dir=/opt/sentinellan/shared/backups
test "$(readlink -f "$backup_dir")" = "$backup_dir"
while IFS= read -r name; do
    [[ "$name" =~ ^sentinellan-[0-9]{8}T[0-9]{6}Z\.dump$ ]]
    test -f "$backup_dir/$name.cms"
    test ! -L "$backup_dir/$name"
    rm -f -- "$backup_dir/$name"
done < /opt/sentinellan/shared/security-20261003/legacy-backups
echo 'Verified legacy plaintext backup files removed; encrypted copies retained.'
