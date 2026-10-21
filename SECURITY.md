# Security policy

SentinelLAN supports authorized endpoint management. The current source is maintained on `main`; no long term support release is promised. This policy reflects the October 2026 source.

## Report a vulnerability

Use GitHub's private vulnerability reporting feature for this repository. Do not open a public issue with exploit details, credentials, private keys, or personal data. Include the affected commit, trust boundary, a minimal reproduction with dummy data, and expected/observed behavior. Maintainers coordinate the fix and disclosure.

For an exposed deployment, revoke affected credentials and follow [the incident workflow](docs/security/incident-workflow.md). Never test a system without authorization.

## Security boundaries

- Human roles are `Admin` and `Employee`; Agent authentication is separate. Migration `20261009065038_RetireLegacyRoles` locks unsupported roles (including `Technician` and `PlatformOwner`), changes security stamps, and revokes refresh sessions and activation tokens without granting replacement privileges.
- Production commands require **RSA-PSS/SHA-256 with at least 3072-bit keys**, versioned `R2` envelopes and a key ID. Only the server holds `SENTINELLAN_COMMAND_PRIVATE_KEY_PEM`; Agents receive `SENTINELLAN_COMMAND_PUBLIC_KEY` and the matching key ID. Command HMAC fallback is limited to Development without RSA configuration. Agent HTTP requests instead use dedicated per-device HMAC keys, a ±60-second timestamp, a persisted one-use nonce and an exact body hash; the original secret bootstrap closes after the first signed proof. See [ADR 0029](docs/adr/0029-agent-request-signatures.md).
- Keep secrets outside Git in environment variables/user secrets; separate command signing, access-token signing, vault and data-encryption keys. Sensitive support data uses authenticated encryption at rest. Attachment lists select metadata and a persisted plaintext byte count; only an authorized download loads image content.
- API policies, use cases and EF global tenant filters enforce tenant scope and Employee ownership/assignment. Filters do not replace authorization or provide PostgreSQL row-level security.
- Optional host monitoring reads a bounded local JSON snapshot from a read-only mount. A root-owned systemd collector produces it. The API has no SSH credentials or Docker socket and exposes no VPS restart/reboot operations. Only the configured operating organization's Admin can read host status; see [host monitoring](docs/deployment/vps-operations.md).
- Forwarded headers run before authentication/rate limiting. Configure `SENTINELLAN_KNOWN_PROXIES` and/or bounded `SENTINELLAN_KNOWN_NETWORKS` for the immediate proxy. Defaults trust loopback only. Unknown peers cannot supply client IP/scheme; one hop is consumed and paired header chains must have equal length. Do not expose API/container ports directly.
- Commands require allow-list, tenant/device checks, reason, confirmation, expiry, nonce, signature and audit. `SimulateLock`/`SimulateNetworkIsolation` always simulate; real Windows/Linux lock/isolation additionally requires explicit lab/device authorization. See [execution limits](docs/guides/real-agent-execution.md).
- Collect technical telemetry only. Support images are employee-selected and consented. Audit is append-only through the application, with no independent WORM guarantee.

See the [threat model](docs/security/threat-model.md) for operational limits and validation requirements.

## Optional distributed services

Redis backplane uses a deployment-specific channel prefix; keep it private and authenticated. Rate limiting remains per process and needs ingress limits when scaling. S3/MinIO attachment objects use AES-GCM with tenant/request/attachment AAD, and authorized downloads remain behind the API. Back up the bucket, database and encryption keys together. Production ASP.NET runtime and self-contained Agent releases require 10.0.13 or later. [Deployment details and limits](docs/guides/product-upgrade.md).
