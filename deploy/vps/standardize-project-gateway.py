#!/usr/bin/python3
"""Migrate the owner's legacy gateway ports; leave other projects/databases alone.

Run plan first, then apply --state <printed backup directory>. The two game
servers must have no established connections. Their running image IDs are
pinned for the port-only replacement. All original files remain recoverable.
"""
import argparse
import datetime
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import re
import subprocess
import time

BACKUPS = Path("/var/backups/sentinellan-project-gateway")
GAMES = (("monopoly-game-server-1", "monopoly", 3110, 9004),
         ("exxplore-kittens-game-server-1", "exxplore-kittens", 3105, 9005))
NGINX = Path("/etc/nginx")


def run(args, timeout=30):
    result = subprocess.run(args, capture_output=True, text=True, timeout=timeout)
    if result.returncode:
        raise RuntimeError("Command failed: " + args[0] + " (" + str(result.returncode) + ")")
    return result.stdout


def containers():
    return json.loads(run(["docker", "inspect"] + run(["docker", "ps", "-aq"]).split()))


def baseline():
    return {c["Name"].lstrip("/"): {"id": c["Id"], "image": c["Image"],
            "started": c["State"]["StartedAt"], "state": c["State"]["Status"],
            "restarts": c["RestartCount"]} for c in containers()}


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def replace_required(text, old, new):
    if old not in text:
        raise RuntimeError("Expected gateway layout changed: " + old)
    return text.replace(old, new)


def game_connections(container):
    output = run(["nsenter", "-t", str(container["State"]["Pid"]), "-n", "ss", "-H", "-nt", "state", "established"])
    return sum(any(field.endswith(":3001") for field in line.split()[:4]) for line in output.splitlines())


def host_ip():
    routes = json.loads(run(["ip", "-j", "-4", "route", "show", "default"]))
    device = routes[0]["dev"]
    addresses = json.loads(run(["ip", "-j", "-4", "addr", "show", "dev", device, "scope", "global"]))
    values = [info["local"] for interface in addresses for info in interface["addr_info"] if info["family"] == "inet"]
    if len(values) != 1 or ipaddress.ip_address(values[0]).is_loopback:
        raise RuntimeError("Expected one primary IPv4 address")
    return values[0]


def save(state, directory):
    path = directory / "state.json"
    path.write_text(json.dumps(state, indent=2))
    path.chmod(0o600)


def validate_nginx_changes(changes, directory):
    # Materialize only configuration text reported by nginx -T, never keys.
    candidate = directory / "nginx-candidate"
    sections = re.split(r"(?m)^# configuration file (.+):\n", run(["nginx", "-T"]))
    files = {sections[i]: sections[i + 1] for i in range(1, len(sections), 2)}
    files.update({name: content for name, content in changes.items() if name.startswith(str(NGINX) + "/")})
    for name, content in files.items():
        original = Path(name)
        if not str(original).startswith(str(NGINX) + "/"):
            continue
        content = changes.get(str(original.resolve()), content)
        destination = candidate / original.relative_to(NGINX)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(content.replace(str(NGINX) + "/", str(candidate) + "/"))
    run(["nginx", "-t", "-c", str(candidate / "nginx.conf")])
    for name, content in changes.items():
        if name.endswith(".py"):
            compile(content, name, "exec")


def plan():
    data = containers()
    by_name = {c["Name"].lstrip("/"): c for c in data}
    address = host_ip()
    directory = BACKUPS / datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    directory.mkdir(parents=True, mode=0o700)
    changes = {}
    games = []
    for name, project, old, new in GAMES:
        c = by_name[name]
        if not c["State"]["Running"] or game_connections(c):
            raise RuntimeError("Game is unavailable or has active connections: " + name)
        config = c["Config"]["Labels"]["com.docker.compose.project.config_files"]
        path = Path(config)
        if "," in config or not path.is_file() or c["Config"]["Labels"]["com.docker.compose.project"] != project:
            raise RuntimeError("Unexpected game Compose configuration")
        changes[str(path)] = replace_required(path.read_text(), "127.0.0.1:" + str(old) + ":3001", "127.0.0.1:" + str(new) + ":3001")
        pin = directory / (project + "-image.yaml")
        pin.write_text("services:\n  game-server:\n    image: " + c["Image"] + "\n    pull_policy: never\n")
        pin.chmod(0o600)
        games.append({"name": name, "project": project, "config": config, "pin": str(pin), "port": new})
        deploy = path.parent if project == "monopoly" else path.parent / "deploy"
        for candidate in deploy.rglob("*"):
            if candidate.is_file() and candidate.suffix in (".sh", ".conf", ".py", ".md"):
                content = candidate.read_text()
                if str(old) in content:
                    changes[str(candidate)] = content.replace(str(old), str(new))
        if project == "exxplore-kittens":
            installer = deploy / "install-nginx.py"
            content = changes.get(str(installer), installer.read_text())
            needle = "    shutil.copyfile(release / 'nginx/kittens-9000.conf', vhost)"
            if needle in content:
                addition = "    vhost_text = (release / 'nginx/kittens-9000.conf').read_text()\n    if Path('/etc/nginx/snippets/project-gateway-http-listen.conf').exists():\n        vhost_text = vhost_text.replace('    listen 9000;\\n    listen [::]:9000;', '    include /etc/nginx/snippets/project-gateway-http-listen.conf;')\n    vhost.write_text(vhost_text)"
                changes[str(installer)] = content.replace(needle, addition)
    listen = "    listen 127.0.0.1:9000;\n    listen " + address + ":9000;\n    listen [::]:9000;\n"
    changes[str(NGINX / "snippets/project-gateway-http-listen.conf")] = listen
    for filename in ("port_9000.conf", "kittens-9000.conf"):
        path = NGINX / "sites-available" / filename
        content = path.read_text()
        content = re.sub(r"    listen 9000( default_server)?;\n    listen \[::\]:9000(?: default_server)?;",
                         lambda m: listen.replace(":9000;", ":9000 default_server;") if m.group(1) else "    include /etc/nginx/snippets/project-gateway-http-listen.conf;\n", content)
        if re.search(r"listen\s+9000", content):
            raise RuntimeError("Unexpected wildcard gateway listener")
        if filename == "kittens-9000.conf":
            content = replace_required(content, "127.0.0.1:3105", "127.0.0.1:9005")
        else:
            content = content.replace("proxy_set_header X-Real-IP $remote_addr;", "proxy_set_header X-Real-IP $project_gateway_client_ip;")
            content = content.replace("proxy_set_header X-Forwarded-Proto $scheme;", "proxy_set_header X-Forwarded-Proto $project_gateway_scheme;")
            content += "\nserver {\n    include /etc/nginx/snippets/project-gateway-http-listen.conf;\n    server_name manager.zney295.id.vn;\n    access_log off;\n    location / { return 308 https://manager.zney295.id.vn$request_uri; }\n}\n"
        changes[str(path)] = content
    for snippet, old, new in (("monopoly-locations.conf", 3110, 9004), ("kittens-locations.conf", 3105, 9005)):
        path = NGINX / "snippets" / snippet
        changes[str(path)] = replace_required(path.read_text(), "127.0.0.1:" + str(old), "127.0.0.1:" + str(new))
    for filename in ("sentinellan-manager.conf", "sentinellan.conf"):
        path = NGINX / "sites-available" / filename
        changes[str(path)] = replace_required(path.read_text(), "https://127.0.0.1:9003", "https://127.0.0.2:9000")
    path = NGINX / "sites-available/voice-public.conf"
    changes[str(path)] = replace_required(path.read_text(), "proxy_pass http://127.0.0.1:9002;", "proxy_pass http://127.0.0.1:9000;")
    changes[str(NGINX / "conf.d/project-gateway-forwarded.conf")] = "map $remote_addr $project_gateway_client_ip {\n    default $remote_addr;\n    127.0.0.1 $http_x_real_ip;\n    ::1 $http_x_real_ip;\n}\nmap \"$remote_addr:$http_x_forwarded_proto\" $project_gateway_scheme {\n    default $scheme;\n    \"127.0.0.1:https\" https;\n    \"::1:https\" https;\n}\n"
    changes[str(NGINX / "project-gateway-tls.conf")] = (Path(__file__).parent / "nginx.gateway-tls.conf").read_text()
    path = NGINX / "nginx.conf"
    content = path.read_text()
    include = "include /etc/nginx/project-gateway-tls.conf;"
    if include in content:
        raise RuntimeError("Gateway TLS listener is already installed")
    changes[str(path)] = content + "\n# Owner projects: separate loopback TLS gateway on port 9000.\n" + include + "\n"
    originals = {name: Path(name).read_text() if Path(name).exists() else None for name in changes}
    state = {"changes": changes, "originals": originals, "games": games, "containers": baseline(), "host_ip": address,
             "untouched_files": {str(p): digest(p) for p in [NGINX / "sites-available/default", NGINX / "sites-available/motmebanh.conf", Path("/opt/sentinellan/shared/.env"), Path("/home/ubuntu/exxplore-kittens/.env")]},
             "volumes": run(["docker", "volume", "ls", "--format", "{{.Name}}|{{.Driver}}"])}
    validate_nginx_changes(changes, directory)
    save(state, directory)
    print("Plan and rollback files: " + str(directory), flush=True)
    print("Monopoly 3110 -> 9004; Kittens 3105 -> 9005; HTTP gateway :9000; loopback TLS gateway 127.0.0.2:9000 -> 9003.", flush=True)


def write_changes(changes):
    for name, content in changes.items():
        path = Path(name)
        if content is None:
            path.unlink(missing_ok=True)
        else:
            path.write_text(content)


def game_compose(game):
    return ["docker", "compose", "-p", game["project"], "-f", game["config"], "-f", game["pin"]]


def verify(state):
    after = baseline()
    targets = {game["name"] for game in state["games"]}
    if {k: v for k, v in after.items() if k not in targets} != {k: v for k, v in state["containers"].items() if k not in targets}:
        raise RuntimeError("A container outside the two game servers changed")
    if any(after[name]["image"] != state["containers"][name]["image"] for name in targets):
        raise RuntimeError("Game image changed")
    if any(digest(path) != checksum for path, checksum in state["untouched_files"].items()):
        raise RuntimeError("An excluded configuration or environment changed")
    if run(["docker", "volume", "ls", "--format", "{{.Name}}|{{.Driver}}"] ) != state["volumes"]:
        raise RuntimeError("Docker volumes changed")
    run(["nginx", "-t"])
    for game in state["games"]:
        run(["curl", "--max-time", "10", "-fsS", "http://127.0.0.1:" + str(game["port"]) + "/health"])
    for path in ("/monopoly/health", "/kittens/health"):
        run(["curl", "--max-time", "10", "-fsS", "-H", "Host: beatsync-server.zney295.id.vn", "http://127.0.0.1:9000" + path])
    run(["curl", "--max-time", "10", "-fsS", "http://127.0.0.1:9000/gateway-health", "-H", "Host: unknown.invalid"])
    run(["curl", "--max-time", "10", "-fsS", "--resolve", "manager.zney295.id.vn:443:127.0.0.1", "https://manager.zney295.id.vn/health/ready"])
    sockets = run(["ss", "-H", "-lntp"])
    if any(":" + str(port) + " " in sockets for port in (3105, 3110)):
        raise RuntimeError("Old game host ports still listen")
    if "127.0.0.2:9000" not in sockets or "127.0.0.1:9000" not in sockets:
        raise RuntimeError("Gateway listeners are missing")
    print("Verified game ports/images, Nginx routes, TLS readiness, excluded projects/configuration and volumes.", flush=True)


def reload_without_old_listener(state):
    # Existing wildcard sockets cannot be replaced by specific bindings in one
    # reload. Briefly close only :9000 listeners; other sites and established
    # connections survive the graceful reload. Never restart shared Nginx.
    for filename in ("port_9000.conf", "kittens-9000.conf"):
        path = str(NGINX / "sites-available" / filename)
        content = state["originals"][path]
        content = re.sub(r"listen (?:9000|\[::\]:9000)( default_server)?;", lambda m: "listen " + ("[::1]" if "[::]" in m.group(0) else "127.0.0.1") + ":9008" + (m.group(1) or "") + ";", content)
        Path(path).write_text(content)
    Path(NGINX / "nginx.conf").write_text(state["originals"][str(NGINX / "nginx.conf")])
    run(["nginx", "-t"])
    run(["systemctl", "reload", "nginx"])
    for _ in range(30):
        sockets = run(["ss", "-H", "-lnt"])
        if not any(":9000 " in line for line in sockets.splitlines()):
            return
        time.sleep(0.1)
    raise RuntimeError("Previous gateway listener did not close")


def apply(state, directory):
    if baseline() != state["containers"] or any(Path(path).read_text() != text for path, text in state["originals"].items() if text is not None):
        raise RuntimeError("Deployment baseline changed; create a new plan")
    by_name = {c["Name"].lstrip("/"): c for c in containers()}
    if any(game_connections(by_name[game["name"]]) for game in state["games"]):
        raise RuntimeError("A game has active connections; refusing replacement")
    for port in (9004, 9005, 9008):
        if run(["ss", "-H", "-lntu", "sport", "=", str(port)]).strip():
            raise RuntimeError("Target port is already occupied: " + str(port))
    try:
        for game in state["games"]:
            Path(game["config"]).write_text(state["changes"][game["config"]])
            run(game_compose(game) + ["config", "--quiet"])
            run(game_compose(game) + ["up", "-d", "--no-deps", "--no-build", "--pull", "never", "--force-recreate", "game-server"], 90)
        reload_without_old_listener(state)
        write_changes(state["changes"])
        run(["nginx", "-t"])
        run(["systemctl", "reload", "nginx"])
        for _ in range(30):
            if "127.0.0.2:9000" in run(["ss", "-H", "-lnt"]):
                break
            time.sleep(0.1)
        verify(state)
        state["applied"] = True
        save(state, directory)
    except Exception:
        rollback(state, directory)
        raise


def rollback(state, directory):
    by_name = {c["Name"].lstrip("/"): c for c in containers()}
    if any(game_connections(by_name[game["name"]]) for game in state["games"]):
        raise RuntimeError("A game has active connections; refusing rollback replacement")
    # First close the specific :9000 sockets to recover the old wildcard ones.
    if "127.0.0.2:9000" in run(["ss", "-H", "-lnt"]):
        reload_without_old_listener(state)
    write_changes(state["originals"])
    for game in state["games"]:
        run(game_compose(game) + ["up", "-d", "--no-deps", "--no-build", "--pull", "never", "--force-recreate", "game-server"], 90)
    run(["nginx", "-t"])
    run(["systemctl", "reload", "nginx"])
    state["rolled_back"] = True
    save(state, directory)
    print("Previous ports and Nginx configuration restored.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("plan", "apply", "verify", "rollback"))
    parser.add_argument("--state")
    args = parser.parse_args()
    if os.geteuid() != 0:
        raise SystemExit("Run as root")
    import fcntl
    with Path("/opt/sentinellan/shared/admin-vps-deploy.lock").open("a") as lock:
        fcntl.flock(lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        if args.mode == "plan":
            plan()
        else:
            if not args.state:
                parser.error("--state is required for this mode")
            directory = Path(args.state).resolve()
            if directory.parent != BACKUPS or not directory.is_dir():
                raise SystemExit("Invalid state directory")
            state = json.loads((directory / "state.json").read_text())
            if args.mode == "verify":
                verify(state)
            else:
                globals()[args.mode](state, directory)
