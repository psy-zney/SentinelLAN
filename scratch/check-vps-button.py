import subprocess

result = subprocess.run(['docker', 'exec', 'sentinellan-prod-platform-1', 'grep', '-rl', 'Thêm máy chủ', '/app/apps/platform/.next/static/chunks'], capture_output=True)
print('Deployed platform bundle contains the Add Server button:', result.returncode == 0)
if result.returncode != 0:
    raise SystemExit(1)
