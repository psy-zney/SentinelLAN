# VPS monitoring deployment verification — 2026-10-03

Deployed release: /opt/sentinellan/releases/20261002-vps-monitor-220227, selected by /opt/sentinellan/current. The platform now automatically shows vps-cong without storing a personal SSH private key or registering a duplicate SSH node.

Verified production state: 14 Docker containers running, one stopped. Host Nginx listens on 80, 443 and 9000. SentinelLAN's Docker gateway publishes 127.0.0.1:3180 to its TLS port 8443. The API's actual listening port is 8443; its Docker-declared 8080 port is displayed separately as a declaration. Published UDP ranges and separate IPv4/IPv6 interfaces remain distinct.

The root-owned collector timer is active and its last service execution succeeded. It writes atomic snapshots every minute. API reads the directory read-only, as UID 1654, without a Docker socket. All nine SentinelLAN containers still use non-root users, read-only root filesystems, no-new-privileges and dropped capabilities. Production checks confirmed snapshots contain no configured passwords, signing/vault keys or Docker environment/command fields. Unauthorized users and company admins are denied the host status endpoint.

Validation completed:

- Linux .NET solution: 248 tests passed (architecture 1, domain 5, application 87, Agent 66, integration 89).
- After fixing Docker inspection for containers without a healthcheck: all 22 focused VPS integration tests passed. The actual Docker Go template also executed successfully against the host's containers.
- Platform: lint and strict typecheck passed; 9 tests passed.
- Shared web package: lint and strict typecheck passed; 34 tests passed.
- Collector: two socket/port parsing tests passed on Windows and Linux.
- API and platform production Docker builds passed. All four public HTTPS readiness/login checks passed.
- Real production owner sign-in, automatic host data, actual/published ports, search, refresh, desktop and mobile checks passed. Mocked stale and unavailable API responses preserved the last sample and showed warnings; these fixtures did not alter production host data.
- Database, company/employee web apps and unrelated application containers retained their identities during deployment.

Windows compiled the backend, but the local Application Control policy blocked loading the API test assembly. The full backend suite was run successfully in an isolated Linux SDK container instead; production database credentials and data were not supplied to that container.

No database migration, reboot, service restart requested through the portal, employee enrollment or device command was performed. The new host panel observes technical state only. Existing confirmed and audited SSH operations remain separate. HTTPS/TLS, database TLS and existing backup controls were preserved. Snapshot freshness does not prove public port reachability; firewall reachability is not inferred.
