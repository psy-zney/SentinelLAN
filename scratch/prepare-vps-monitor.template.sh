#!/usr/bin/env bash
set -euo pipefail
release=/opt/sentinellan/releases/RELEASE_NAME
prior=$(readlink -f /opt/sentinellan/current)
test -d "$prior"
test ! -e "$release"
mkdir -m 755 "$release"
cp -a "$prior/." "$release/"
tar -xzf /home/ubuntu/sentinellan-vps-monitor.tar.gz -C "$release"
cd "$release"
python3 -B -m unittest discover -s deploy/vps/tests
bash deploy/vps/compose-secure.sh config --quiet
docker run --rm --name sentinellan-vps-monitor-tests --memory=3g --cpus=2 --pids-limit=256 \
  --mount "type=bind,src=$release,dst=/source,readonly" --tmpfs /work:rw,size=2g \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 -e NUGET_PACKAGES=/work/nuget \
  mcr.microsoft.com/dotnet/sdk:10.0.400 bash -c \
  'set -e; cp /source/Directory.Build.props /source/Directory.Packages.props /source/global.json /source/SentinelLAN.slnx /work/; mkdir /work/apps; cp -a /source/apps/backend /source/apps/agent /work/apps/; cd /work; dotnet test SentinelLAN.slnx --verbosity quiet'
touch "$release/.vps-monitor-tests-passed"
echo "VERIFIED_RELEASE=$release"
