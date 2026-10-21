import importlib.util
import json
import subprocess

spec = importlib.util.spec_from_file_location("collector", "/opt/sentinellan/current/deploy/vps/collect-host-status.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
process = subprocess.run(module.DOCKER + ["ps", "-aq", "--no-trunc"], capture_output=True, text=True, timeout=8)
ids = process.stdout.split()
result = subprocess.run(module.DOCKER + ["inspect", "--format", module.INSPECT] + ids, capture_output=True, text=True, timeout=15)
print("Inspection exit:", result.returncode, "containers:", len(ids))
print(result.stderr[:2500])
snapshot = json.load(open("/opt/sentinellan/shared/host-monitor/status.json"))
print("Warnings:", snapshot["warnings"])
print("Actual API sockets:", next(row["listeningPorts"] for row in snapshot["runtime"]["containers"] if row["name"] == "sentinellan-prod-api-1"))
