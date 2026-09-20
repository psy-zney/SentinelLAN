# ADR 0003: MVP command verification

Status: Accepted; implementation reconciled 2026-09-06.

The allow-list is `ShowNotification`, `CollectTelemetryNow`, `RefreshPolicy`, `SimulateLock`, and `SimulateNetworkIsolation`. All handlers are safe MVP simulations/scheduling acknowledgements. There is no arbitrary shell, real lock, network isolation, or RestartService adapter. An environment flag alone does not enable an adapter that does not exist.

Command creation requires an Admin/Technician in the device tenant, `confirmed: true`, a nonblank reason (at most 1000 characters), and a 30–900 second validity (default 120). Revoked devices are rejected. The server generates the nonce.

HMAC-SHA256 signs a JSON array containing, in order: command ID, organization ID, device ID, actor ID, type, reason, issued-at Unix milliseconds, expires-at Unix milliseconds, and nonce. Millisecond normalization survives PostgreSQL timestamp precision. The Agent independently verifies the same envelope using `SENTINELLAN_SIGNING_KEY`. Without that key it continues telemetry but does not consume commands. Existing commands signed using the previous envelope must expire and be reissued; deploy API and Agent together.

The shared key is an MVP limitation: a compromised Agent holding the key could forge signatures. Production requires asymmetric signatures, provisioned/pinned public keys and rotation. The nonce cache and retry buffers are process-local; restart-safe replay protection and durable delivery remain Week 5 work.

Polling uses optimistic concurrency on command status. A result is accepted only for the authenticated device's delivered, unexpired command. Retries preserve the first stored result. Creation audit records remain unchanged; completion appends a new record including command ID. EF rejects audit UPDATE/DELETE; database privileges and an external immutable sink remain pending.

Enrollment uses optimistic concurrency on UsedAt. PostgreSQL transactions/unique indexes protect multi-record writes; the Development InMemory provider serializes these writes because it has no rollback. InMemory tests do not certify PostgreSQL behavior.
