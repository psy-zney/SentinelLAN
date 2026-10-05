#!/usr/bin/env bash
# Root-owned production wrapper. No decrypted database dump is written to disk.
set -euo pipefail
umask 077
exec >> /opt/sentinellan/shared/backup.log 2>&1
backup_dir=/opt/sentinellan/shared/backups
tls_dir=/opt/sentinellan/shared/tls/20261003/backup-signing
mkdir -p "$backup_dir"
chmod 700 "$backup_dir"
stamp=$(date -u +%Y%m%dT%H%M%SZ)
target="$backup_dir/sentinellan-$stamp.dump.cms"
temporary="$backup_dir/.sentinellan-$stamp-$$.tmp"
trap 'rm -f -- "$temporary"' EXIT
encrypt_stream() {
    openssl cms -sign -binary -nodetach -md sha384 -outform DER -stream \
      -signer "$tls_dir/backup-signing.crt" -inkey "$tls_dir/backup-signing.key" \
    | openssl cms -encrypt -binary -aes-256-cbc -outform DER -stream \
      -recip "$tls_dir/backup-recipient.crt" -keyopt rsa_padding_mode:oaep -keyopt rsa_oaep_md:sha256 \
      -out "$1"
}
docker exec sentinellan-prod-postgres-1 sh -c \
  'export PGPASSWORD="$POSTGRES_PASSWORD"; exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc --no-owner --no-privileges' \
  | encrypt_stream "$temporary"
test -s "$temporary"
openssl cms -cmsout -inform DER -in "$temporary" -noout
chmod 600 "$temporary"
mv -- "$temporary" "$target"
trap - EXIT
echo "Encrypted and signed backup created: $target"
# Save recovery configuration separately; this includes the key for encrypted server credentials.
temporary="$backup_dir/.config-$stamp-$$.tmp"
trap 'rm -f -- "$temporary"' EXIT
encrypt_stream "$temporary" < /opt/sentinellan/shared/.env
test -s "$temporary"
chmod 600 "$temporary"
mv -- "$temporary" "$backup_dir/config-$stamp.env.cms"
trap - EXIT
echo 'Encrypted recovery configuration created separately.'
