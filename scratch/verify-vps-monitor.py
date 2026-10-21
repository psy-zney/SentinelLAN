import http.cookiejar
import json
from pathlib import Path
import urllib.error
import urllib.request

values = dict(line.split("=", 1) for line in Path("/opt/sentinellan/shared/.env").read_text().splitlines() if "=" in line and not line.startswith("#"))
origin = "https://" + values["PUBLIC_DOMAIN"]
route = "/api/v1/platform/host/status"


def request(client, path, body=None):
    data = None if body is None else json.dumps(body).encode()
    return client.open(urllib.request.Request(origin + path, data=data, headers={"Content-Type": "application/json", "X-SentinelLAN-CSRF": "1"}), timeout=20)


def rejected(client):
    try:
        request(client, route)
        raise AssertionError("Unauthorized monitoring request accepted")
    except urllib.error.HTTPError as error:
        assert error.code in (401, 403)


rejected(urllib.request.build_opener())
print("PASS anonymous host monitoring rejected")
for owner in (True, False):
    client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    prefix = "/api/v1/platform/auth" if owner else "/api/v1/auth"
    payload = {"email": values["PLATFORM_OWNER_EMAIL" if owner else "BOOTSTRAP_ADMIN_EMAIL"],
               "password": values["PLATFORM_OWNER_PASSWORD" if owner else "BOOTSTRAP_ADMIN_PASSWORD"]}
    if not owner:
        payload["organizationCode"] = values["BOOTSTRAP_ORG_CODE"]
    logged_in = False
    try:
        assert request(client, prefix + "/login", payload).status == 200
        logged_in = True
        if not owner:
            rejected(client)
            print("PASS company admin host monitoring rejected")
            continue
        response = request(client, route)
        assert response.headers.get("Cache-Control") == "no-store"
        raw = response.read().decode()
        data = json.loads(raw)
        assert data["configured"] and data["available"] and not data["stale"], data["message"]
        snapshot = data["snapshot"]
        assert snapshot["name"] == "vps-cong"
        assert snapshot["host"] == "149.118.50.176"
        assert not snapshot["warnings"], snapshot["warnings"]
        assert snapshot["runtime"]["dockerAvailable"]
        assert not snapshot["runtime"]["dockerError"]
        containers = snapshot["runtime"]["containers"]
        by_name = {row["name"]: row for row in containers}
        for name in ("api", "company", "employee", "platform", "postgres", "nginx", "company-tls", "employee-tls", "platform-tls"):
            assert by_name["sentinellan-prod-" + name + "-1"]["state"] == "running"
        api = by_name["sentinellan-prod-api-1"]
        assert any(port["port"] == 8443 and port["protocol"] == "tcp" for port in api["listeningPorts"])
        assert not any(port["port"] == 8080 for port in api["listeningPorts"])
        gateway = by_name["sentinellan-prod-nginx-1"]
        assert any(port["hostIp"] == "127.0.0.1" and port["hostPort"] == 3180 and port["containerPort"] == 8443 for port in gateway["ports"])
        host_ports = snapshot["listeningPorts"]
        assert any(port["port"] == 9000 and port["protocol"] == "tcp" and "nginx" in (port["process"] or "") for port in host_ports)
        for secret in ("POSTGRES_PASSWORD", "SIGNING_KEY", "SERVER_VAULT_KEY", "PLATFORM_OWNER_PASSWORD"):
            assert values[secret] not in raw
        assert not any(key in raw for key in ('"Env"', '"environment"', '"Cmd"', '"privateKey"'))
        print("PASS owner: fresh host resources, all {} Docker containers, actual API 8443, gateway 127.0.0.1:3180 -> 8443 and host nginx 9000".format(len(containers)))
        print("PASS snapshot contains no configuration secrets or container command/environment fields")
        print("Host RAM {:.1f}% | disk {:.1f}% | checked {}".format(snapshot["ramPercent"], snapshot["diskPercent"], snapshot["capturedAtUtc"]))
    finally:
        if logged_in:
            assert request(client, prefix + "/logout", {}).status == 204
