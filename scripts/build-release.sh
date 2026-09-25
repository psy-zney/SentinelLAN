#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-1.0.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

echo "============================================================"
echo "  SentinelLAN — Building Production Release v$VERSION        "
echo "============================================================"

RELEASE_DIR="$ROOT/dist/release-v$VERSION"
rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR/agents" "$RELEASE_DIR/deploy" "$RELEASE_DIR/api"

echo "[*] Publishing Linux Agent x64 Binary..."
dotnet publish apps/agent/src/SentinelLAN.Agent/SentinelLAN.Agent.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$RELEASE_DIR/agents"

echo "[*] Publishing Backend ASP.NET Core API..."
dotnet publish apps/backend/src/SentinelLAN.Api/SentinelLAN.Api.csproj \
  -c Release \
  -o "$RELEASE_DIR/api"

echo "[*] Packaging Deployment Scripts..."
cp -r deploy/vps/* "$RELEASE_DIR/deploy/"
cp -r deploy/agent/* "$RELEASE_DIR/deploy/"

echo "[*] Generating SHA256 Checksums..."
cd "$RELEASE_DIR"
find . -type f \( -name "*.dll" -o -name "SentinelLAN.Agent" -o -name "*.sh" -o -name "*.yaml" -o -name "*.conf" \) -exec sha256sum {} + > SHA256SUMS.txt

echo "[*] Creating tar.gz Archive..."
cd "$ROOT/dist"
tar -czvf "sentinellan-v$VERSION.tar.gz" "release-v$VERSION"

echo "============================================================"
echo "  Release Package Complete: dist/sentinellan-v$VERSION.tar.gz"
echo "============================================================"
