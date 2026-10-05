#!/usr/bin/env bash
set -euo pipefail
directory=/opt/sentinellan/shared/tls/20261003
failed=0
for certificate in "$directory"/{api/api,postgres/postgres,gateway/gateway,company/server,employee/server,platform/server}.crt; do
    if ! openssl x509 -in "$certificate" -checkend 2592000 -noout >/dev/null; then
        echo "Internal TLS certificate expires within 30 days: $certificate" >&2
        failed=1
    fi
done
exit "$failed"
