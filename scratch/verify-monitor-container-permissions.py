import json
from pathlib import Path
import stat
import subprocess

template = '{"User":{{json .Config.User}},"ReadOnly":{{json .HostConfig.ReadonlyRootfs}},"Caps":{{json .HostConfig.CapDrop}},"Security":{{json .HostConfig.SecurityOpt}},"Mounts":{{json .Mounts}}}'
for app in ("api", "company", "employee", "platform", "postgres", "nginx", "company-tls", "employee-tls", "platform-tls"):
    value = json.loads(subprocess.check_output(["docker", "inspect", "--format", template, "sentinellan-prod-" + app + "-1"], text=True))
    assert value["User"] and value["User"].split(":")[0] not in ("root", "0")
    assert value["ReadOnly"] and "ALL" in value["Caps"]
    assert "no-new-privileges:true" in value["Security"]
    assert not any(mount["Destination"].endswith("docker.sock") for mount in value["Mounts"])
    if app == "api":
        monitor = next(mount for mount in value["Mounts"] if mount["Destination"] == "/run/sentinellan-host-monitor")
        assert not monitor["RW"]
        assert value["User"].split(":")[0] == "1654"
directory = Path("/opt/sentinellan/shared/host-monitor")
snapshot = directory / "status.json"
assert stat.S_IMODE(directory.stat().st_mode) == 0o750
assert stat.S_IMODE(snapshot.stat().st_mode) == 0o640
assert snapshot.stat().st_uid == 0 and snapshot.stat().st_gid == 1654
subprocess.run(["systemctl", "is-active", "--quiet", "sentinellan-host-monitor.timer"], check=True)
snapshot_data = json.loads(snapshot.read_text())
print("PASS all nine Sentinel containers: non-root, read-only root, capabilities dropped, no-new-privileges and no Docker socket")
print("PASS API snapshot mount read-only; root-owned state directory/file permissions; host monitor timer active")
print("Docker running:", sum(row["state"] == "running" for row in snapshot_data["runtime"]["containers"]), "total:", len(snapshot_data["runtime"]["containers"]))
