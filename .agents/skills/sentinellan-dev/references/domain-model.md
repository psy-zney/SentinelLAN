# Domain model reference

All tenant-owned records carry `OrganizationId` and UTC timestamps. Organizations contain departments, users, devices, policies, alerts, security events, and audit events. Enrollment tokens are single-use and stored as hashes. A Device owns a credential, heartbeats, telemetry snapshots, policy assignments, commands, and command results. Command state moves from Pending to Delivered to Succeeded/Failed/Expired; results are idempotent by command ID.
