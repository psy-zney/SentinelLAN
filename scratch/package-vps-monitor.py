from pathlib import Path
from datetime import datetime, timezone
import tarfile
import tempfile

root = Path(__file__).resolve().parents[1]
name = datetime.now(timezone.utc).strftime("%Y%m%d-vps-monitor-%H%M%S")
archive = Path(tempfile.gettempdir()) / ("sentinellan-" + name + ".tar.gz")
files = set()
for folder, extensions in [
    ("apps/backend", {".cs", ".csproj", ".json"}), ("apps/agent", {".cs", ".csproj", ".json"}),
    ("apps/platform/src", {".ts", ".tsx", ".css"}), ("apps/platform/tests", {".ts"}),
    ("packages/web-ui/src", {".ts", ".tsx", ".css"}), ("deploy/vps", {".yaml", ".conf", ".sh", ".py", ".service", ".timer"})]:
    for file in (root / folder).rglob("*"):
        if file.is_file() and file.suffix in extensions and not set(file.relative_to(root).parts) & {"bin", "obj", "node_modules", "certs", "__pycache__"}:
            if "secret" not in file.name.lower() and "credential" not in file.name.lower():
                files.add(file)
for file in ["Directory.Build.props", "Directory.Packages.props", "global.json", "SentinelLAN.slnx", "package.json", "package-lock.json", ".editorconfig"]:
    files.add(root / file)
with tarfile.open(archive, "w:gz") as tar:
    for file in sorted(files):
        tar.add(file, arcname=file.relative_to(root).as_posix(), recursive=False)
for source, destination in [("prepare-vps-monitor.template.sh", "prepare-vps-monitor.sh"), ("deploy-vps-monitor.template.sh", "deploy-vps-monitor.sh")]:
    content = (root / "scratch" / source).read_text(encoding="utf-8").replace("RELEASE_NAME", name)
    (root / "scratch" / destination).write_text(content, encoding="utf-8", newline="\n")
(root / "scratch/vps-monitor-release.txt").write_text(name + "\n" + str(archive), encoding="utf-8")
print(name)
print(archive)
print("Source files:", len(files))
