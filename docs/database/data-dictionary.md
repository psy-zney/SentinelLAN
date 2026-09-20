# Data dictionary

All IDs are UUIDs and timestamps are UTC. Tenant records include `OrganizationId`; organizations also have a unique login `Code`. User passwords use versioned PBKDF2-SHA256 hashes. Refresh tokens, enrollment tokens, and device credentials store hashes only.

`RefreshSession` records the user, tenant, token family, expiry, revocation/replacement state and an optimistic-concurrency version. A rotated-token reuse revokes all active sessions in that family. `Device.AssignedUserId` is nullable and limits Employee access to the assigned device.

Device telemetry is limited to CPU/RAM/disk percentages and declared OS/Agent versions. `TelemetrySnapshot` records technical utilization and is exposed to the UI via `TelemetrySnapshotDto`. `DeviceHeartbeat` enforces idempotency via unique `(DeviceId, IdempotencyKey)` index; duplicate submissions are acknowledged idempotently without creating redundant telemetry snapshots.

`DeviceEnrollmentToken` enforces single-use token consumption via `TryUse(now)` which transitions `UsedAt` from null to the UTC timestamp; tokens with `now >= ExpiresAt` or non-null `UsedAt` are rejected. Enrollment attempts are audited in `AuditLog`: successful enrollments log `AgentEnrolled` with `Outcome = "Success"`, and invalid reuse or expired attempts log `AgentEnrollmentFailed` with `Outcome = "Failed"` and the specific failure reason.

Commands store actor, device, type, reason, issue/expiry, nonce, signature, status, and one idempotent result. Audit logs are application append-only; production should additionally restrict database UPDATE/DELETE privileges.

## Verification update — 2026-09-06

- EnrollmentTokens.UsedAt and Commands.Status are optimistic concurrency tokens. New migrations record these model changes; no merged migration was edited.
- A heartbeat requires a 1–128 character nonblank idempotency key, bounded OS/Agent version strings, and finite CPU/RAM/disk values in [0, 100].
- Command creation adds the `confirmed` boolean (default false). Unconfirmed requests return 400; a revoked device returns 409.
- Results before delivery or after expiry return 409. Duplicate receipts return 200 without replacing the original result. Completion audit appends CommandCompleted:<command-id-N>:<type>.
- CPU percentages are interval system usage; RAM is physical system usage; disk is system/root volume usage. Initial CPU sampling may return 0 until a measurable interval exists.
