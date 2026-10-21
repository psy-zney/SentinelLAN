# Hosting VPS monitoring through a read-only snapshot

The hosting VPS must appear automatically in Super Admin, even when no SSH VPS node has been registered. Application-container CPU and memory are not host metrics; Docker EXPOSE metadata is not evidence of a listening socket.

A fixed root-owned systemd timer collects technical host state every minute. It reads /proc, disk usage, systemd state, host sockets, selected Docker inspection fields and resource counters. Network namespaces are inspected with the host's ss executable using a numeric container PID; container commands are never executed. Environment variables, process arguments, credentials and logs are excluded from the snapshot.

The collector atomically replaces a bounded JSON file in a root-owned directory. The API runs without Docker socket access, extra capabilities or SSH keys and mounts only this directory read-only. Infrastructure implements the snapshot-reader Application port. The Application service and API both require PlatformOwner. GET /api/v1/platform/host/status sets Cache-Control: no-store. No database migration is required.

Samples older than three minutes or over one minute in the future are flagged as stale. Missing, invalid or oversized snapshots report unavailable instead of reporting zero utilization or a healthy VPS. The browser retains the last sample on request failures and displays its timestamp. Polling pauses while the page is hidden and requests are cancelled when leaving the view.

The display separates actual listening sockets, published host-to-container bindings and declared-only container ports. Public address bindings describe listening scope; firewall reachability is not inferred. Containers sharing a network namespace show namespace sockets with an explicit note. Consecutive ranges are grouped only when protocol, address and mapping offset match.

The host collector is privileged because the host owns Docker and network namespaces. Protect its script, environment file and service definitions as root-owned files. The web endpoint never accepts commands, paths or target PIDs. Existing confirmed and audited SSH operations remain a separate workflow.
