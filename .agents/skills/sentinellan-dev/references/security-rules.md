# Security rules

- Authenticate people and devices separately; store only password/refresh/enrollment/device-secret hashes.
- Enforce organization scope and permission policies in Application/API boundaries; do not trust route IDs alone.
- Commands require an allowed type, actor, reason, issue/expiry timestamps, unique nonce, signature, and audit trail.
- Reject expired, replayed, unsigned, cross-tenant, or duplicate-result commands.
- Collect only online state, CPU, RAM, disk, OS version, Agent version, and published policy events.
- `SimulateLock` and `SimulateNetworkIsolation` never alter the OS. Real adapters require explicit lab configuration and authorization.
