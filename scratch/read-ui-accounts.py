import json
from pathlib import Path

values = dict(line.split("=", 1) for line in Path("/opt/sentinellan/shared/.env").read_text().splitlines() if "=" in line and not line.startswith("#"))
print(json.dumps({
    "platform": {"email": values["PLATFORM_OWNER_EMAIL"], "password": values["PLATFORM_OWNER_PASSWORD"]},
    "company": {"email": values["BOOTSTRAP_ADMIN_EMAIL"], "password": values["BOOTSTRAP_ADMIN_PASSWORD"], "code": values["BOOTSTRAP_ORG_CODE"]}
}))
