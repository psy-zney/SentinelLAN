"""Bounded local Agent experiment with loopback fault injection and OS metrics.

Uses a fresh Development/InMemory API and a disposable Agent identity. It never
issues commands or changes firewall, policy, services, installed applications or
the physical network. This is not PostgreSQL/TLS or production OS acceptance.
"""
from __future__ import annotations

import argparse
import csv
import ctypes as C
from ctypes import wintypes as W
from datetime import datetime, timezone
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import http.client
import http.cookiejar
import json
import math
import os
from pathlib import Path
import secrets
import socket
import statistics
import subprocess
import threading
import time
import urllib.request


def utc():
    return datetime.now(timezone.utc).isoformat()


class Counters(C.Structure):
    _fields_ = [("cb", W.DWORD), ("PageFaultCount", W.DWORD)]+[(name, C.c_size_t) for name in
                ["PeakWorkingSetSize", "WorkingSetSize", "QuotaPeakPagedPoolUsage", "QuotaPagedPoolUsage",
                 "QuotaPeakNonPagedPoolUsage", "QuotaNonPagedPoolUsage", "PagefileUsage", "PeakPagefileUsage", "PrivateUsage"]]


def process_metrics(pid):
    kernel = C.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.restype = W.HANDLE
    kernel.OpenProcess.argtypes = [W.DWORD, W.BOOL, W.DWORD]
    kernel.CloseHandle.argtypes = [W.HANDLE]
    kernel.GetProcessTimes.argtypes = [W.HANDLE, *([C.POINTER(W.FILETIME)]*4)]
    psapi = C.WinDLL("psapi", use_last_error=True)
    psapi.GetProcessMemoryInfo.argtypes = [W.HANDLE, C.POINTER(Counters), W.DWORD]
    handle = kernel.OpenProcess(0x1000 | 0x10, False, pid)
    if not handle:
        raise C.WinError(C.get_last_error())
    try:
        info = Counters()
        info.cb = C.sizeof(info)
        times = [W.FILETIME() for _ in range(4)]
        if not psapi.GetProcessMemoryInfo(handle, C.byref(info), info.cb) or not kernel.GetProcessTimes(handle, *[C.byref(t) for t in times]):
            raise C.WinError(C.get_last_error())
        seconds = sum((t.dwHighDateTime << 32) + t.dwLowDateTime for t in times[2:])/10_000_000
        return seconds, info.WorkingSetSize/1024**2, info.PrivateUsage/1024**2
    finally:
        kernel.CloseHandle(handle)


def experiment(repo: Path, output: Path, duration=300, outage=180):
    if duration < outage+90 or outage < 30:
        raise ValueError("Retain at least 90 seconds connected and a 30-second outage")
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    identity_dir = output / "private-agent-state"
    identity_dir.mkdir()
    api = repo / "apps/backend/src/SentinelLAN.Api/bin/Debug/net10.0/SentinelLAN.Api.dll"
    agent = repo / "apps/agent/src/SentinelLAN.Agent/bin/Debug/net10.0/SentinelLAN.Agent.dll"
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        api_port = probe.getsockname()[1]
    state = {"blocked": False, "phase": "warmup", "heartbeats": [], "transport": []}
    lock = threading.Lock()

    class Relay(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def do_GET(self):
            self.forward()

        def do_POST(self):
            self.forward()

        def forward(self):
            if self.headers.get("Transfer-Encoding", "").lower() == "chunked":
                chunks, total = [], 0
                while True:
                    size = int(self.rfile.readline().split(b";", 1)[0].strip(), 16)
                    if size == 0:
                        while self.rfile.readline().strip():
                            pass
                        break
                    total += size
                    if total > 8 * 1024 * 1024:
                        self.send_error(413)
                        return
                    chunks.append(self.rfile.read(size))
                    self.rfile.read(2)
                body = b"".join(chunks)
            else:
                body = self.rfile.read(int(self.headers.get("Content-Length", 0)))
            if self.headers.get("Upgrade"):
                self.send_error(426)
                return
            with lock:
                blocked, phase = state["blocked"], state["phase"]
            if blocked:
                self.send_response(503)
                self.send_header("Content-Length", "0")
                self.end_headers()
                return
            start = time.perf_counter()
            connection = http.client.HTTPConnection("127.0.0.1", api_port, timeout=25)
            try:
                headers = {k: v for k, v in self.headers.items() if k.lower() not in ("host", "connection", "transfer-encoding", "content-length", "expect")}
                connection.request(self.command, self.path, body=body, headers=headers)
                response = connection.getresponse()
                data = response.read()
                elapsed = (time.perf_counter()-start)*1000
                with lock:
                    state["transport"].append({"method": self.command, "path": self.path.split('?')[0], "status": response.status})
                self.send_response(response.status)
                for key, value in response.getheaders():
                    if key.lower() not in ("connection", "transfer-encoding", "content-length"):
                        self.send_header(key, value)
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)
                if self.path == "/api/v1/agent/heartbeat" and response.status < 300:
                    payload = json.loads(body)
                    with lock:
                        state["heartbeats"].append({"received_at_utc": utc(), "elapsed_ms": elapsed,
                                                     "phase": phase, "idempotencyKey": payload.get("idempotencyKey"),
                                                     "collected_at_utc": payload.get("collectedAt")})
            finally:
                connection.close()

    relay = ThreadingHTTPServer(("127.0.0.1", 0), Relay)
    relay.daemon_threads = True
    threading.Thread(target=relay.serve_forever, daemon=True).start()
    base = f"http://127.0.0.1:{api_port}"
    password, token, signing = (secrets.token_hex(32) for _ in range(3))
    processes, samples = [], []
    def start_process(assembly, extra):
        env = os.environ.copy()
        # Never inherit a connection to a real database or production identity.
        env.update({"ConnectionStrings__SentinelLAN": "", "ASPNETCORE_ENVIRONMENT": "Development",
                    "DOTNET_ENVIRONMENT": "Development", "Logging__LogLevel__Default": "Error",
                    "SENTINELLAN_LAB_EXECUTION": "false", "SENTINELLAN_LAB_DEVICE_ID": "",
                    "SENTINELLAN_ALLOW_SERVICE_RESTART": "false", "SENTINELLAN_ALLOW_POLICY_CHANGES": "false",
                    "SENTINELLAN_ALLOW_APPROVED_APP_INSTALL": "false", "SENTINELLAN_ALLOW_AGENT_MAINTENANCE": "false"})
        env.update(extra)
        process = subprocess.Popen(["dotnet", str(assembly)], cwd=repo, env=env,
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        processes.append(process)
        return process
    result = None
    try:
        api_process = start_process(api, {"ASPNETCORE_URLS": base, "SENTINELLAN_LOGIN_ORG_CODE": "demo",
                     "SENTINELLAN_DEMO_ADMIN_PASSWORD": password, "SENTINELLAN_ENROLLMENT_TOKEN": token,
                     "SENTINELLAN_SIGNING_KEY": signing, "SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY": secrets.token_hex(32),
                     "SENTINELLAN_SERVER_VAULT_KEY": secrets.token_hex(32)})
        for _ in range(60):
            if api_process.poll() is not None:
                raise RuntimeError("Isolated API exited before readiness")
            try:
                with urllib.request.urlopen(base+"/health/live", timeout=1) as response:
                    if response.status == 200:
                        break
            except OSError:
                time.sleep(.5)
        else:
            raise TimeoutError("API did not become ready")
        cookie_jar = http.cookiejar.CookieJar()
        client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookie_jar))
        login = urllib.request.Request(base+"/api/v1/auth/login", json.dumps({"organizationCode": "demo", "email": "admin@sentinellan.local", "password": password}).encode(), {"Content-Type": "application/json"})
        client.open(login, timeout=10).close()
        agent_process = start_process(agent, {"SENTINELLAN_API_URL": f"http://127.0.0.1:{relay.server_port}",
                        "SENTINELLAN_SIGNING_KEY": signing, "SENTINELLAN_ENROLLMENT_TOKEN": token,
                        "SENTINELLAN_AGENT_DATA_DIR": str(identity_dir)})
        for _ in range(90):
            if (identity_dir / "identity.json").exists() and state["heartbeats"]:
                break
            if agent_process.poll() is not None:
                raise RuntimeError("Agent exited before the first real heartbeat")
            time.sleep(.5)
        else:
            raise RuntimeError("Agent did not enroll and produce a heartbeat within 45 seconds")
        start_time = time.perf_counter()
        started_at = utc()
        previous_time, previous_cpu = start_time, process_metrics(agent_process.pid)[0]
        disconnected_at = reconnected_at = None
        while (elapsed := time.perf_counter()-start_time) < duration:
            if api_process.poll() is not None or agent_process.poll() is not None:
                raise RuntimeError("A lab process exited; no experiment success is asserted")
            phase = "baseline" if elapsed < 45 else "outage" if elapsed < 45+outage else "recovery"
            with lock:
                state["phase"], state["blocked"] = phase, phase == "outage"
            if phase == "outage" and disconnected_at is None:
                disconnected_at = utc()
            if phase == "recovery" and reconnected_at is None:
                reconnected_at = utc()
            cpu, ram, private = process_metrics(agent_process.pid)
            now = time.perf_counter()
            single_cpu = (cpu-previous_cpu)/(now-previous_time)*100
            samples.append({"time_utc": utc(), "elapsed_seconds": now-start_time, "phase": phase,
                            "cpu_one_core_percent": single_cpu, "cpu_all_logical_processors_percent": single_cpu/(os.cpu_count() or 1),
                            "working_set_mib": ram, "private_bytes_mib": private})
            previous_time, previous_cpu = now, cpu
            time.sleep(2)
        identity = json.loads((identity_dir / "identity.json").read_text(encoding="utf-8-sig"))
        device_id = identity.get("DeviceId") or identity["deviceId"]
        with client.open(base+f"/api/v1/devices/{device_id}/telemetry?limit=500", timeout=10) as response:
            persisted = json.load(response)
        heartbeats = state["heartbeats"]
        if not heartbeats or not any(h["phase"] == "recovery" for h in heartbeats):
            raise RuntimeError("Recovery heartbeats were not observed")
        stable = [s for s in samples if s["elapsed_seconds"] >= 30]
        ordinary = [h["elapsed_ms"] for h in heartbeats if h["phase"] in ("baseline", "recovery")]
        queued = [h for h in heartbeats if h["phase"] == "recovery" and h["collected_at_utc"] and h["collected_at_utc"] < reconnected_at]
        keys = [h["idempotencyKey"] for h in heartbeats]
        result = {"status": "measured", "started_at_utc": started_at, "ended_at_utc": utc(),
                  "environment": "Windows; two real .NET processes; HTTP loopback relay; Development/InMemory; no OS actions",
                  "duration_seconds": time.perf_counter()-start_time, "outage_seconds_configured": outage,
                  "disconnect_at_utc": disconnected_at, "reconnect_at_utc": reconnected_at,
                  "metric_samples": len(samples), "logical_processors": os.cpu_count(),
                  "agent_working_set_mib_min": min(s["working_set_mib"] for s in stable),
                  "agent_working_set_mib_max": max(s["working_set_mib"] for s in stable),
                  "agent_cpu_all_processors_percent_mean": statistics.mean(s["cpu_all_logical_processors_percent"] for s in stable),
                  "heartbeat_roundtrip_ms_median": statistics.median(ordinary),
                  "heartbeat_roundtrip_ms_p95": sorted(ordinary)[math.ceil(len(ordinary)*.95)-1],
                  "successful_heartbeat_requests": len(heartbeats), "unique_successful_keys": len(set(keys)),
                  "backfilled_samples_observed": len(queued), "api_samples_returned": len(persisted),
                  "assembly_sha256": {"api": hashlib.sha256(api.read_bytes()).hexdigest(), "agent": hashlib.sha256(agent.read_bytes()).hexdigest()},
                  "limitations": ["A relay 503 simulates unavailable transport; no physical network or firewall was interrupted.",
                                  "One local run; not a PostgreSQL/TLS, long-term, crash-restart or multi-device benchmark.",
                                  "Latency is proxy-to-API response time; not Agent-to-dashboard latency.",
                                  "Offline sampling follows Agent retry backoff, not a fixed five-second cadence."]}
    except Exception as error:
        status_path = identity_dir / "enrollment-status.json"
        status = json.loads(status_path.read_text(encoding="utf-8-sig")) if status_path.exists() else None
        (output / "failure.json").write_text(json.dumps({"error": str(error), "enrollment": status,
                                                        "transport": state["transport"]}, indent=2), encoding="utf-8")
        raise
    finally:
        for process in reversed(processes):
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait(timeout=5)
        relay.shutdown(); relay.server_close()
        # Delete only this run's regular private state files, never a computed tree.
        for path in identity_dir.iterdir():
            if path.is_file() and not path.is_symlink():
                path.unlink()
    if result is not None:
        with (output / "process-samples.csv").open("w", newline="", encoding="utf-8") as file:
            writer = csv.DictWriter(file, fieldnames=list(samples[0]))
            writer.writeheader(); writer.writerows(samples)
        (output / "heartbeats.json").write_text(json.dumps(state["heartbeats"], indent=2), encoding="utf-8")
        (output / "measurements.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
        print(json.dumps(result))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--duration", type=int, default=300)
    parser.add_argument("--outage", type=int, default=180)
    args = parser.parse_args()
    experiment(args.repo, args.output, args.duration, args.outage)
