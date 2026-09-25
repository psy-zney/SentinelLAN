# ADR 0005: MVP administration and explicit simulation

Date: 2026-09-20. Status: Accepted for this implementation.

## Context

Enrollment, assigned-device visibility and credential rejection already exist, but administrators cannot issue enrollment tokens, create users, assign devices or revoke credentials through the dashboard. The singular employee endpoint assumes one device. Existing command handling can turn `SimulateLock` into a real OS action under a flag and invokes Linux service control from Agent Core. The command parameter is omitted from the signature.

## Decisions

1. Add Admin-only management use cases through Application services and ports. API endpoints translate HTTP requests and outcomes; Infrastructure implements persistence, hashing and random token generation. Validate tenant and role in Application as well as endpoint authorization. Use the existing cookie/CSRF boundary.
2. Store normalized email and password hashes. Generate 32 random bytes for enrollment tokens, persist only their hash, and return the raw token only in the creation response with `Cache-Control: no-store`. The dashboard keeps it only in component memory until dismissed.
3. Retain the existing one-device employee experience. Enforce at most one non-revoked device per organization/user through validation and a filtered unique PostgreSQL index. Keep optimistic concurrency for a device. Existing duplicate assignments must be resolved by an operator before applying the migration; migration must not delete or silently reassign user data.
4. Revocation preserves the device and its history, revokes its credential, clears assignment and appends audit in one save. Subsequent authenticated Agent calls fail. Repeating a successful revocation is idempotent. Revocation does not change the remote OS.
5. All MVP commands are simulations. `SimulateLock` and `SimulateNetworkIsolation` always leave the OS unchanged, regardless of flags. `RestartService` validates the existing service allow-list and simulates. Agent Core must not import OS APIs or spawn processes. Future real lab actions require distinct action names and Infrastructure adapters with their own explicit authorization; an environment flag must not reinterpret a simulation.
6. Append `Parameter` (including JSON null when absent) as the tenth field of the HMAC JSON array. Preserve the order and representation of the existing nine fields. API and Agent must be deployed together; pending commands signed with the old envelope must expire and be reissued. No legacy-signature fallback is added because it would omit parameter integrity.
7. Preserve existing DTO properties and add `assignedUserId` and `isRevoked` to DeviceDto. The canonical policy count is `assignedDeviceCount`. SignalR events invalidate data and views read the authoritative state through the typed API client.
8. Manual alerts validate referenced devices through a tenant-scoped Application contract and attribute audit to the authenticated operator. Name resolution must also enforce tenant scope for old invalid references. Acknowledge and resolve retries must not append duplicate audit entries.

## Consequences and verification

The API gains POST `/users`, POST `/enrollment-tokens`, PUT `/devices/{id}/assignment`, and POST `/devices/{id}/revoke` under `/api/v1`; the current request schema is in Development OpenAPI and the [use cases](../use-cases/use-cases.md) describe acceptance scenarios. The existing tenant query pattern is retained; these changes do not claim database row-level security or a cross-module rewrite of legacy endpoints.

Required checks cover permission denial, foreign tenant IDs, invalid/null input, duplicate email, assignment races, credential rejection after revocation, audit without secrets, signature parameter tampering and simulation under lab flags. PostgreSQL migration/race verification must be reported separately from InMemory tests. Protected Agent queue, nonce and pending-result stores are implemented for Production; host-level verification, automatic alert rules and production key rotation remain open work.
