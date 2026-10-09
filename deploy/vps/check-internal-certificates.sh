#!/usr/bin/env bash
set -euo pipefail
directory=${TLS_DIRECTORY:?Set TLS_DIRECTORY}
failed=0
for certificate in "$directory"/{api/api,postgres/postgres,gateway/gateway,company/server,employee/server}.crt; do
    if ! openssl x509 -in "$certificate" -checkend 2592000 -noout >/dev/null; then
        echo "Internal TLS certificate expires within 30 days: $certificate" >&2
        failed=1
    fi
done
exit "$failed"
