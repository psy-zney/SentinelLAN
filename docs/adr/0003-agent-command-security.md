# ADR 0003: MVP command verification

Status: Accepted; implementation reconciled 2026-09-06.

The allow-list is `ShowNotification`, `CollectTelemetryNow`, `RefreshPolicy`, `SimulateLock`, `SimulateNetworkIsolation`, and `RestartService`. All handlers return safe simulation acknowledgements. There is no arbitrary shell, real lock, network isolation, or service restart adapter. An environment flag alone does not enable an adapter that does not exist.

Command creation requires an Admin/Technician in the device tenant, `confirmed: true`, a nonblank reason (at most 1000 characters), and a 30–900 second validity (default 120). Revoked devices are rejected. The server generates the nonce.

HMAC-SHA256 signs a JSON array containing, in order: command ID, organization ID, device ID, actor ID, type, reason, issued-at Unix milliseconds, expires-at Unix milliseconds, nonce, and parameter. Millisecond normalization survives PostgreSQL timestamp precision. The Agent independently verifies the same envelope using `SENTINELLAN_SIGNING_KEY`. Without that key it continues telemetry but does not consume commands. Existing commands signed using the previous envelope must expire and be reissued; deploy API and Agent together.

The shared key is an MVP limitation: a compromised Agent holding the key could forge signatures. Production requires asymmetric signatures, provisioned/pinned public keys and rotation. In Production, accepted command nonces are stored in `command-nonces.dat`, protected with purpose-separated DPAPI on Windows or AES-GCM with `SENTINELLAN_AGENT_STORE_KEY` on non-Windows hosts. The cache is pruned to unexpired entries, each with at most 15 minutes remaining, and is capped at 8,192 entries. Corrupt or unreadable cache data prevents Agent startup; a failed atomic write prevents command acceptance. Development continues to use an in-memory cache.

This local cache cannot detect file deletion or rollback by a host-level actor; either can remove replay history while a command remains valid. The API/Agent shared signing key also remains an MVP limitation. Production persists one pending command result in a separate protected file and retries it after restart before polling another command. A crash between nonce acceptance and persisting the result, or a host-level deletion or rollback of that file, can still lose the receipt. The telemetry queue has its own protected store.

Polling uses a 30-second delivery lease and optimistic concurrency on command status and lease expiry. A lost poll response can be redelivered with the same signed envelope until the command expires; see [command delivery](../architecture/command-delivery.md). A result is accepted only for the authenticated device's delivered, unexpired command. Retries preserve the first stored result. Creation audit records remain unchanged; completion appends a new record including command ID. EF rejects audit UPDATE/DELETE; database privileges and an external immutable sink remain pending.

Enrollment uses optimistic concurrency on UsedAt. PostgreSQL transactions/unique indexes protect multi-record writes; the Development InMemory provider serializes these writes because it has no rollback. InMemory tests do not certify PostgreSQL behavior.
