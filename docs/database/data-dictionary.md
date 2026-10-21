# Data dictionary

All IDs are UUIDs and timestamps are UTC. Tenant records include `OrganizationId`; organizations also have a unique login `Code`. User passwords use salted, versioned PBKDF2-SHA256 hashes with 600,000 iterations for new hashes; older hashes are rehashed after successful login. Refresh/enrollment tokens and original device secrets store hashes; dedicated Agent request keys are encrypted with tenant/device AAD.

`RefreshSession` records the user, tenant, token family, expiry, revocation/replacement state and an optimistic-concurrency version. A rotated-token reuse revokes all active sessions in that family. `Device.AssignedUserId` is nullable and limits Employee access to the assigned device.

Device telemetry contains CPU/RAM/disk, declared OS/Agent versions and optional endpoint protection observations with collection timestamps. Hardware inventory is stored in SpecificationsJson.inventory and InventoryCapturedAt. `TelemetrySnapshot` records technical utilization and is exposed to the UI via `TelemetrySnapshotDto`. `DeviceHeartbeat` enforces idempotency via unique `(DeviceId, IdempotencyKey)` index; duplicate submissions are acknowledged idempotently without creating redundant telemetry snapshots.

`DeviceEnrollmentToken` enforces single-use token consumption via `TryUse(now)` which transitions `UsedAt` from null to the UTC timestamp; tokens with `now >= ExpiresAt` or non-null `UsedAt` are rejected. Enrollment attempts are audited in `AuditLog`: successful enrollments log `AgentEnrolled` with `Outcome = "Success"`, and invalid reuse or expired attempts log `AgentEnrollmentFailed` with `Outcome = "Failed"` and the specific failure reason.

Commands store actor, device, type, reason, issue/expiry, nonce, signature, status, and one idempotent result. Audit logs are application append-only; production should additionally restrict database UPDATE/DELETE privileges.

`SelfServiceAttachment.Size` stores the original image byte count independently of encrypted `Content`. Migration `20261021131510_AttachmentMetadataSize` backfills both legacy plaintext and v1 AES-GCM envelopes (33 bytes of overhead). List queries select only ID, request, filename, content type, size and timestamp; upload limits use a tenant/request-scoped COUNT. Only authorized downloads load the binary column. The external attachment DTO remains unchanged.

`User.Status` is `PendingActivation`, `Active`, or `Locked`. Authentication accepts only active users. `SecurityStamp` changes when activation establishes a new password.

`AccountActivationToken` is tenant-owned and binds one user to a SHA-256 token hash, expiry, issuer, `UsedAt`, `RevokedAt`, and an application-managed concurrency row version. A partial unique index permits at most one unconsumed, unrevoked token per tenant/user. Activation updates the user, consumes the token, revokes sibling tokens, and appends an audit event in one save transaction.

`DeviceQrLabel` is tenant-owned and binds a device to a high-entropy opaque code hash. It stores only a short non-secret prefix for support/audit, optional expiry, revocation, last authenticated scan time, and a concurrency row version. A partial unique index permits at most one unrevoked label per tenant/device. The plaintext code is returned only when generated or rotated.

Public QR responses intentionally omit internal UUIDs, assigned-user identity, full serial number, detailed location, purchase cost, telemetry, and secrets. Authenticated resolution is tenant- and assignment-scoped.


## Verification update — 2026-09-06

- EnrollmentTokens.UsedAt and Commands.Status are optimistic concurrency tokens. New migrations record these model changes; no merged migration was edited.
- A heartbeat requires a 1–128 character nonblank idempotency key, bounded OS/Agent version strings, and finite CPU/RAM/disk values in [0, 100].
- Command creation adds the `confirmed` boolean (default false). Unconfirmed requests return 400; a revoked device returns 409.
- Results before delivery or after expiry return 409. Duplicate receipts return 200 without replacing the original result. Completion audit appends CommandCompleted:<command-id-N>:<type>.
- CPU percentages are interval system usage; RAM is physical system usage; disk is system/root volume usage. Initial CPU sampling may return 0 until a measurable interval exists.

## Product upgrade 2026 10 21

Migration 20261021155323_ProductUpgrade removes unused VpsNodes/VpsActionReservations and adds:

| Model | Data and invariants |
|---|---|
| OrganizationMonitoringSettings | One row per tenant, numeric thresholds/durations, revision concurrency |
| AdministrativeRequestNonce | Tenant + nonce unique, actor, operation, UTC expiry; sensitive mutation journal |
| AgentRequestNonce | Device + nonce unique, expiry; signed-request replay prevention across API instances |
| DeviceCredential | Encrypted RequestSigningKeyEnvelope, RequestSigningConfirmedAt, RequestKeyRevision concurrency |
| Department | Tenant, mutable name, default PolicyId, revision; User/Device DepartmentId nullable, composite tenant FKs |
| DeviceSoftwareObservation | Tenant/device/rule/source unique, detection/version, captured time and rule revision |
| ProhibitedSoftwareRule | Configured MSI ProductCode/UpgradeCode, active flag, revision |
| SelfServiceCatalogApp | Optional ProductCode/UpgradeCode and DetectionRevision |
| PreventiveMaintenanceRun | Tenant + UTC date unique; WorkOrder.AutomaticKey unique when non-null |
| OperationalOutboxMessage | Tenant/device/entity/kind, processed timestamp; durable wake/alert notification delivery |
| SelfServiceAttachment | Optional ObjectKey after encrypted S3 offload; Size stays the original plaintext size |

Technical retention also clears expired request/admin nonces, software observations/security events older than 30 days, and processed outbox messages older than one day. Audit is retained. Pending outbox messages are retried, never expired by cleanup. Migration Down cannot restore removed VPS records. The generated PostgreSQL schema is the detailed source of column/index definitions.
