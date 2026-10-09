#!/usr/bin/python3
"""Fixed, read-only host probe. Never collect Docker environments, commands or logs.

Root runs this locally through a timer; the API receives only a read-only JSON file.
No arguments from API users, Docker socket mount in the API, or SSH key are required.
"""
import datetime
from decimal import Decimal
import json
import os
import platform
import posixpath
import re
import shutil
import subprocess
import tempfile
import time

DIRECTORY = "/opt/sentinellan/shared/host-monitor"
DOCKER = ["/usr/bin/docker", "--host", "unix:///var/run/docker.sock"]
INSPECT = ('{"Id":{{json .Id}},"Pid":{{.State.Pid}},'
           '"RestartPolicy":{{json .HostConfig.RestartPolicy.Name}},'
           '"Health":{{with (index .State "Health")}}{{json .Status}}{{else}}null{{end}},'
           '"Ports":{{json .NetworkSettings.Ports}},"NetworkMode":{{json .HostConfig.NetworkMode}},'
           '"Labels":{{json .Config.Labels}}}')


def run(args, timeout=8):
    result = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                            timeout=timeout, check=False, text=True, env={"PATH": "/usr/sbin:/usr/bin:/sbin:/bin", "LC_ALL": "C"})
    return result.returncode, result.stdout


def percent(value):
    try:
        return round(max(0, float(value.rstrip("%"))), 1)
    except (ValueError, AttributeError):
        return None


def socket_rows(output):
    rows = set()
    for line in output.splitlines():
        fields = line.split()
        if len(fields) < 6 or fields[0] not in ("tcp", "udp"):
            continue
        address, sep, number = fields[4].rpartition(":")
        if not sep or not number.isdigit() or not 0 < int(number) <= 65535:
            continue
        names = re.findall(r'\("([^\"]+)"', " ".join(fields[6:]))
        process = ", ".join(sorted(set(names))) or None
        rows.add((address.strip("[]"), int(number), fields[0], process))
    return [{"address": a, "port": p, "protocol": t, "process": n}
            for a, p, t, n in sorted(rows, key=lambda row: (row[2], row[1], row[0], row[3] or ""))]


def port_bindings(value):
    rows = []
    for key, bindings in (value or {}).items():
        number, _, protocol = key.partition("/")
        if not number.isdigit() or not 0 < int(number) <= 65535 or protocol not in ("tcp", "udp", "sctp"):
            continue
        for binding in bindings or [{"HostIp": None, "HostPort": None}]:
            host_port = binding.get("HostPort")
            rows.append({"containerPort": int(number), "protocol": protocol,
                         "hostIp": binding.get("HostIp"), "hostPort": int(host_port) if host_port else None})
    return rows


def cpu_times():
    with open("/proc/stat", encoding="ascii") as file:
        values = [int(value) for value in file.readline().split()[1:9]]
    return sum(values), values[3] + values[4]


def parse_size_str(val):
    if not val:
        return 0
    match = re.fullmatch(r"([0-9]+(?:\.[0-9]+)?)\s*([A-Za-z]+)?", str(val).strip())
    if not match:
        return 0
    num = Decimal(match.group(1))
    unit = (match.group(2) or "B").upper()
    factors = {
        "B": 1, "KB": 1000, "K": 1000, "KIB": 1024,
        "MB": 1000**2, "M": 1000**2, "MIB": 1024**2,
        "GB": 1000**3, "G": 1000**3, "GIB": 1024**3,
        "TB": 1000**4, "T": 1000**4, "TIB": 1024**4
    }
    return int(num * factors[unit]) if unit in factors else 0


def storage_targets(inspected, warnings):
    targets = {
        "BeatSync": (["/opt/beatsync", "/home/ubuntu/beatsync"], "repo"),
        "Unified Monitor Agent": (["/opt/unified-monitoring-agent"], "service"),
        "SentinelLAN": (["/opt/sentinellan"], "repo"),
        "Kitchen Explore": (["/home/ubuntu/exxplore-kittens", "/var/www/exxplore-kittens"], "repo"),
        "Monopoly (mpoly)": (["/opt/monopoly", "/var/www/monopoly"], "repo"),
        "LiveKit": (["/home/ubuntu/livekit"], "service"),
        "Go SDK / Build Cache": (["/home/ubuntu/go"], "system"),
    }
    project_names = {"sentinellan-prod": "SentinelLAN", "sentinellan": "SentinelLAN",
                     "exxplore-kittens": "Kitchen Explore", "monopoly": "Monopoly (mpoly)",
                     "mot-me-banh": "Mọt Mê Bánh", "livekit": "LiveKit", "beatsync": "BeatSync"}
    allowed_roots = ("/opt", "/srv", "/var/www", "/home/ubuntu")
    seen = set()
    for container in inspected:
        labels = container.get("Labels") or {}
        project = labels.get("com.docker.compose.project")
        directory = labels.get("com.docker.compose.project.working_dir")
        if not isinstance(project, str) or not isinstance(directory, str):
            continue
        if (project, directory) in seen:
            continue
        seen.add((project, directory))
        name = project_names.get(project, project)
        if not directory.startswith("/") or "\n" in directory or "\x00" in directory:
            warnings.append("Đường dẫn thư mục dự án không hợp lệ: " + name + ".")
            continue
        directory = os.path.realpath(directory)
        root = next((root for root in allowed_roots if directory.startswith(root + "/")), None)
        if root is None:
            warnings.append("Thư mục dự án nằm ngoài phạm vi được phép đo: " + name + ".")
            continue
        if not os.path.isdir(directory):
            warnings.append("Chưa đọc được thư mục đang triển khai của " + name + ".")
            continue
        # Compose may run from a deploy subdirectory of a Git checkout.
        candidate = directory
        while candidate != root:
            if os.path.exists(posixpath.join(candidate, ".git")):
                directory = candidate
                break
            candidate = posixpath.dirname(candidate)
        if name not in targets:
            targets[name] = ([], "repo")
        targets[name][0].append(directory)

    resolved = []
    for name, (paths, category) in targets.items():
        existing = sorted({os.path.realpath(path) for path in paths if os.path.isdir(path)}, key=len)
        unique = []
        for path in existing:
            if not any(path == parent or path.startswith(parent + "/") for parent in unique):
                unique.append(path)
        if unique:
            resolved.append((name, unique, category))
    return resolved


def collect_storage(inspected=(), warnings=None):
    warnings = warnings if warnings is not None else []
    items = []
    deadline = time.monotonic() + 20
    try:
        code, output = run(DOCKER + ["system", "df", "--format", "{{json .}}"], 6)
    except (OSError, subprocess.TimeoutExpired):
        code, output = 1, ""
    if code:
        warnings.append("Chưa đo được dung lượng Docker; các số liệu VPS khác vẫn được cập nhật.")
    if code == 0:
        for line in output.splitlines():
            try:
                row = json.loads(line)
                t = row.get("Type", "")
                size_str = row.get("Size", "0B")
                reclaim = row.get("Reclaimable")
                items.append({
                    "name": "Docker " + t,
                    "path": "/var/lib/docker (" + t.lower() + ")",
                    "sizeBytes": parse_size_str(size_str),
                    "category": "docker",
                    "reclaimable": reclaim if reclaim and reclaim not in ("0B (0%)", "0B") else None
                })
            except (ValueError, KeyError, TypeError, AttributeError):
                warnings.append("Một mục dung lượng Docker chưa đọc được.")

    for name, paths, cat in storage_targets(inspected, warnings):
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            warnings.append("Chưa đo hết các thư mục dự án trong thời gian cho phép.")
            break
        try:
            code, out = run(["/usr/bin/du", "-s", "-B1", "--"] + paths, min(4, remaining))
        except (OSError, subprocess.TimeoutExpired):
            code, out = 1, ""
        if code:
            warnings.append("Chưa đo được dung lượng thư mục của " + name + ".")
        if code == 0:
            total_bytes = sum(int(l.split()[0]) for l in out.splitlines() if l.split() and l.split()[0].isdigit())
            if total_bytes > 0:
                items.append({
                    "name": name,
                    "path": ", ".join(paths),
                    "sizeBytes": total_bytes,
                    "category": cat,
                    "reclaimable": None
                })

    items.sort(key=lambda x: x["sizeBytes"], reverse=True)
    return items


def collect(name, host):
    warnings = []
    before = cpu_times()
    time.sleep(1)
    after = cpu_times()
    total = after[0] - before[0]
    cpu = round(max(0, min(100, 100 * (1 - (after[1] - before[1]) / total))), 1) if total else 0
    with open("/proc/meminfo", encoding="ascii") as file:
        memory = {line.split(":")[0]: int(line.split()[1]) * 1024 for line in file}
    memory_total = memory["MemTotal"]
    memory_used = memory_total - memory["MemAvailable"]
    disk = shutil.disk_usage("/")
    with open("/proc/uptime", encoding="ascii") as file:
        uptime = float(file.read().split()[0])
    system_state = run(["/usr/bin/systemctl", "is-system-running"])[1].strip() or "unknown"
    services = []
    for service in ("nginx", "docker", "ssh", "beatsync", "cron", "systemd-resolved", "sentinellan-host-monitor.timer"):
        services.append({"name": service, "activeState": run(["/usr/bin/systemctl", "is-active", service])[1].strip() or "unknown",
                         "startupState": run(["/usr/bin/systemctl", "is-enabled", service])[1].strip() or "unknown"})
    host_sockets = socket_rows(run(["/usr/bin/ss", "-H", "-lntup"])[1])
    containers = []
    docker_error = None
    available = False
    details = {}
    try:
        code, output = run(DOCKER + ["ps", "-a", "--no-trunc", "--size", "--format", "{{json .}}"], 12)
        if code:
            raise RuntimeError("docker unavailable")
        available = True
        rows = [json.loads(line) for line in output.splitlines()]
        ids = [row["ID"] for row in rows if re.fullmatch(r"[a-f0-9]{64}", row["ID"])]
        stats = {}
        if ids:
            code, output = run(DOCKER + ["inspect", "--format", INSPECT] + ids, 12)
            if code:
                warnings.append("Chưa đọc được cổng công bố và cấu hình Docker.")
            else:
                details = {item["Id"]: item for item in (json.loads(line) for line in output.splitlines())}
            code, output = run(DOCKER + ["stats", "--no-stream", "--no-trunc", "--format", "{{json .}}"] + ids, 12)
            if code:
                warnings.append("Chưa đọc được mức sử dụng CPU và RAM của Docker.")
            else:
                stats = {item["ID"]: item for item in (json.loads(line) for line in output.splitlines())}
        namespaces = {}
        for row in rows:
            info = details.get(row["ID"], {})
            labels = info.get("Labels") or {}
            c_name = row.get("Names", "")
            compose_proj = labels.get("com.docker.compose.project")
            if compose_proj:
                proj = compose_proj
            elif "sentinellan" in c_name:
                proj = "sentinellan-prod"
            elif "monopoly" in c_name or "mpoly" in c_name:
                proj = "monopoly"
            elif "kitten" in c_name or "exxplore" in c_name:
                proj = "exxplore-kittens"
            elif "banh" in c_name:
                proj = "mot-me-banh"
            elif "livekit" in c_name:
                proj = "livekit"
            elif "beat" in c_name or "sync" in c_name:
                proj = "beatsync"
            else:
                proj = "other"
            usage = stats.get(row["ID"], {})
            running = row.get("State") == "running"
            listeners = None
            pid = info.get("Pid", 0)
            if running and isinstance(pid, int) and pid > 0:
                try:
                    namespace = os.stat("/proc/{}/ns/net".format(pid)).st_ino
                    if namespace not in namespaces:
                        code, output = run(["/usr/bin/nsenter", "--target", str(pid), "--net", "/usr/bin/ss", "-H", "-lntu"], 3)
                        namespaces[namespace] = socket_rows(output) if code == 0 else None
                    listeners = namespaces[namespace]
                    if listeners is None:
                        warnings.append("Chưa đọc được cổng đang nghe của " + row.get("Names", row["ID"][:12]) + ".")
                except (OSError, subprocess.TimeoutExpired):
                    warnings.append("Container vừa đổi trạng thái: " + row.get("Names", row["ID"][:12]) + ".")
            containers.append({"id": row["ID"], "name": row.get("Names", ""), "image": row.get("Image", ""),
                "state": row.get("State", "unknown"), "status": row.get("Status", "unknown"),
                "health": info.get("Health"), "restartPolicy": info.get("RestartPolicy"),
                "cpuPercent": percent(usage.get("CPUPerc")) if running else None,
                "memoryUsage": usage.get("MemUsage") if running else None,
                "memoryPercent": percent(usage.get("MemPerc")) if running else None,
                "storageUsage": row.get("Size"), "networkIo": usage.get("NetIO") if running else None,
                "blockIo": usage.get("BlockIO") if running else None,
                "ports": port_bindings(info.get("Ports")), "listeningPorts": listeners,
                "networkMode": info.get("NetworkMode"),
                "project": proj})
    except (OSError, subprocess.TimeoutExpired, RuntimeError, ValueError, KeyError):
        docker_error = "Không đọc được đầy đủ trạng thái Docker."
    storage_breakdown = collect_storage(details.values(), warnings)
    return {"name": name, "host": host, "capturedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "osInfo": platform.platform(), "uptimeSeconds": uptime, "cpuPercent": cpu,
        "ramPercent": round(memory_used / memory_total * 100, 1), "diskPercent": round(disk.used / disk.total * 100, 1),
        "memoryTotalBytes": memory_total, "memoryUsedBytes": memory_used, "diskTotalBytes": disk.total, "diskUsedBytes": disk.used,
        "runtime": {"systemState": system_state, "dockerAvailable": available, "dockerError": docker_error,
                    "services": services, "containers": containers, "storageBreakdown": storage_breakdown},
        "listeningPorts": host_sockets, "warnings": warnings}


def main():
    if os.geteuid() != 0:
        raise SystemExit("Host collector must run as root.")
    snapshot = collect(os.environ.get("SENTINELLAN_HOST_NAME", platform.node()), os.environ["SENTINELLAN_HOST_ADDRESS"])
    encoded = json.dumps(snapshot, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    if len(encoded) > 2 * 1024 * 1024:
        raise SystemExit("Host snapshot exceeds the size limit.")
    os.makedirs(DIRECTORY, mode=0o750, exist_ok=True)
    os.chown(DIRECTORY, 0, 1654)
    fd, temporary = tempfile.mkstemp(prefix=".status-", dir=DIRECTORY)
    try:
        with os.fdopen(fd, "wb") as file:
            os.fchmod(file.fileno(), 0o640)
            os.fchown(file.fileno(), 0, 1654)
            file.write(encoded)
            file.flush()
            os.fsync(file.fileno())
        os.replace(temporary, os.path.join(DIRECTORY, "status.json"))
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


if __name__ == "__main__":
    main()
