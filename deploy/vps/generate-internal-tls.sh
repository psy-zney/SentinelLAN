#!/usr/bin/env bash
# Run on the Linux origin as root. Creates a NEW directory and never replaces keys.
set -euo pipefail
umask 077
directory=${1:?Provide a new absolute TLS directory outside the checkout}
database_user=${2:-sentinellan}
[[ $directory = /* && $database_user =~ ^[a-z_][a-z0-9_]{0,62}$ ]] || { echo 'Invalid directory or database user' >&2; exit 1; }
[[ ! -e $directory ]] || { echo 'TLS directory already exists; use a new directory' >&2; exit 1; }
mkdir -p "$directory"/{private-ca,api,postgres,gateway,company,employee}
chmod 755 "$directory" "$directory"/{api,postgres,gateway,company,employee}
openssl req -x509 -newkey rsa:4096 -nodes -sha384 -days 3650 \
  -subj '/CN=SentinelLAN Internal CA' -addext 'basicConstraints=critical,CA:TRUE' \
  -addext 'keyUsage=critical,keyCertSign,cRLSign' \
  -keyout "$directory/private-ca/ca.key" -out "$directory/private-ca/ca.crt" 2>/dev/null
issue() {
  local folder=$1 stem=$2 common_name=$3 usage=$4 owner=$5
  openssl req -new -newkey rsa:3072 -nodes -sha384 -subj "/CN=$common_name" \
    -keyout "$directory/$folder/$stem.key" -out "$directory/private-ca/$folder-$stem.csr" 2>/dev/null
  printf 'basicConstraints=critical,CA:FALSE\nkeyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=%s\nsubjectAltName=DNS:%s\n' "$usage" "$common_name" > "$directory/private-ca/extension.cnf"
  openssl x509 -req -sha384 -days 90 -in "$directory/private-ca/$folder-$stem.csr" \
    -CA "$directory/private-ca/ca.crt" -CAkey "$directory/private-ca/ca.key" -CAcreateserial \
    -extfile "$directory/private-ca/extension.cnf" -out "$directory/$folder/$stem.crt" 2>/dev/null
  cp "$directory/private-ca/ca.crt" "$directory/$folder/ca.crt"
  chown "$owner" "$directory/$folder/$stem.key"
  chmod 600 "$directory/$folder/$stem.key"
  chmod 644 "$directory/$folder/$stem.crt" "$directory/$folder/ca.crt"
}
issue api api api serverAuth 1654:1654
issue postgres postgres postgres serverAuth 70:70
issue api db-client "$database_user" clientAuth 1654:1654
issue gateway gateway gateway serverAuth 101:101
issue company server company serverAuth 101:101
issue employee server employee serverAuth 101:101
echo "Internal TLS certificates created. TLS_DIRECTORY=$directory"
echo 'Certificates expire after 90 days. Store the private CA offline and rotate before expiry.'
