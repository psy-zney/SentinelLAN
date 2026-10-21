# ADR 0029: HMAC request signing for device authentication

Date: 2026-10-21. Status: Accepted.

## Decision

Normal Agent HTTP requests use a device-specific random 32-byte HMAC key. DeviceSecret remains hash-only on the server and proves enrollment/bootstrap; it is not transmitted in heartbeat, polling, policy, result or maintenance requests. The HMAC key is stored in an AES-256-GCM envelope whose AAD binds organization and device, and in the Agent's existing protected identity store.

The `S1` signature covers UTF-8 text joined by LF, without a final LF:

1. `SentinelLAN-Agent-Request-1`
2. Device UUID in lowercase D format
3. Uppercase HTTP method
4. Original escaped request path and query
5. Unix timestamp in seconds, invariant decimal
6. Random nonce UUID in lowercase N format
7. Lowercase hex SHA-256 digest of the exact transmitted body bytes (including an empty body)

Headers carry Device-Id, Timestamp, Nonce and `Signature: S1.<base64 HMAC-SHA256>`. The server accepts timestamps within 60 seconds, limits bodies to 256 KiB, compares signatures in constant time and atomically reserves `(DeviceId, Nonce)` in PostgreSQL. The replay check is shared across API replicas; expired reservations are removed only after their acceptance window. HTTP idempotency keys remain independent, so retries use fresh authentication nonces while preserving business idempotency.

This is a SentinelLAN protocol, not an implementation of the complete RFC 9421 structured-field format. Coverage, replay and TLS considerations follow the concerns described in [RFC 9421](https://www.rfc-editor.org/rfc/rfc9421.html#section-7.2).

## Transition

New enrollment returns the HMAC key alongside the existing identity. Existing Agents prove their original credential once at the bounded TLS bootstrap endpoint and persist the upgraded identity before sending signed traffic. Lost bootstrap responses recover the same key until the first valid signed request confirms possession. After that confirmation, original-secret bootstrap, enrollment recovery and legacy header authentication are closed for that credential; a lost protected identity requires administrator recovery/re-enrollment.

Production requires signed requests by default. An operator may explicitly configure a migration expiry at most seven days ahead; this never permits downgrade after signing has been confirmed. Development fixtures may use legacy authentication until confirmation; `SENTINELLAN_REQUIRE_SIGNED_AGENT_REQUESTS=true` disables that compatibility. Requests containing both signature and secret are rejected.

## Limits and validation

HMAC does not replace TLS, private-key command signing, device revocation or clock synchronization. Request signatures and RSA-PSS command signatures use different keys and directions. A compromised endpoint can report false local observations; this protocol authenticates the sender, not the endpoint's integrity.

Integration tests use the production Agent signer against the API and cover content/method/path/device changes, expiry/future timestamps, replay, encrypted key storage, bootstrap response recovery and downgrade prevention. Live proxy/TLS and PostgreSQL concurrency still require deployment validation.
