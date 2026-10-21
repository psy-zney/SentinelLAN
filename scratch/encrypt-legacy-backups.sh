#!/usr/bin/env bash
set -euo pipefail
umask 077
backup_dir=/opt/sentinellan/shared/backups
tls_dir=/opt/sentinellan/shared/tls/20261003/backup-signing
test "$(readlink -f "$backup_dir")" = "$backup_dir"
manifest=/opt/sentinellan/shared/security-20261003/legacy-backups
: > "$manifest"
for input in "$backup_dir"/sentinellan-*.dump; do
    test -f "$input" || continue
    test ! -L "$input"
    output="$input.cms"
    test ! -e "$output"
    openssl cms -sign -binary -nodetach -md sha384 -outform DER -stream \
        -signer "$tls_dir/backup-signing.crt" -inkey "$tls_dir/backup-signing.key" -in "$input" \
      | openssl cms -encrypt -binary -aes-256-cbc -outform DER -stream \
        -recip "$tls_dir/backup-recipient.crt" -keyopt rsa_padding_mode:oaep -keyopt rsa_oaep_md:sha256 -out "$output"
    test -s "$output"
    basename "$input" >> "$manifest"
done
echo 'Legacy backups encrypted; originals await recovery verification.'
