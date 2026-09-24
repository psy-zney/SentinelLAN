# Use Cases & Operational Scenarios

## 1. Administrative Governance
- **Multi-Tenant Administration**: Manage organization boundaries, assign technician roles, configure telemetry collection intervals, and define compliance policies.
- **Fleet Inventory & Node Management**: Monitor both Windows workstations and Linux Cloud VPS instances (Oracle Cloud / AWS) in a unified "single pane of glass".
- **Emergency Intervention**: Dispatch cryptographically signed emergency commands (`SimulateLock` / `LockWorkStation`, `RestartService`) with mandatory justification reasons.
- **Immutable Audit Inspection**: Review tamper-evident logs attributing every action to actor, device, timestamp, and cryptographic execution outcome.

## 2. Technical Support & SRE
- **Live Health Monitoring**: Observe real-time SignalR streams of CPU, RAM, and Disk utilization across workstations and servers.
- **Incident Triage & Service Recovery**: Triage threshold alerts and safely restart crashed cloud services (`nginx`, `docker`) via allow-listed command dispatch without requiring SSH access.

## 3. Employee Transparency (Privacy-by-Design)
- **Asset Self-Inspection**: Log in to `/my-device` to view the health status and applied policies of their assigned workstation.
- **Privacy Audit Verification**: Inspect the exact metrics collected by IT, with cryptographic certainty and transparency that no keylogging, audio/video monitoring, screen captures, or file scanning occurs.

## 4. Managed Agent Lifecycle
- **Cryptographic Enrollment**: Perform one-time enrollment using a single-use token, generating and securely storing the persistent device credential.
- **Resilient Heartbeat & Deduplication**: Dispatch periodic UTC heartbeats with idempotency keys; gracefully queue offline telemetry in local encrypted storage during outages and flush with exponential backoff.
- **Tamper-Resistant Execution**: Verify server HMAC/signatures, validate nonces against replay attacks, execute allow-listed actions, and return idempotent execution receipts.

## 5. Account Activation & QR Asset Access

- **Admin-created account**: An Admin creates a pending account and receives a cryptographically random activation link exactly once. The database stores only the token hash.
- **Single-use activation**: The invited user sets a password of at least 12 characters. Expired, revoked, replayed, or concurrently consumed tokens are rejected.
- **QR asset lifecycle**: Admins and Technicians generate, rotate, revoke, download, and print an opaque QR label for an authorized device. Raw QR codes are never written to the audit log.
- **Context-aware scan**: Anonymous users receive only a minimal asset card. Admins and Technicians are routed to the tenant-scoped device view; an Employee is routed to `/my-device` only when the device is assigned to that account.
- **Accessible PWA scanner**: `/scan` supports the browser Barcode Detector API, a pinned ZXing fallback, local image upload, and manual code entry. URLs from another origin and invalid schemes are rejected.
- **Employee incident report**: An Employee can create and follow incidents for the assigned device without supplying or changing a device ID.
