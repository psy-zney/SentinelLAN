# Threat Model & Security Architecture (STRIDE & Zero Trust)

SentinelLAN applies **Defense-in-Depth** and **Zero Trust Architecture (NIST SP 800-207)**: *"Never Trust, Always Verify"*. Neither the central server nor the managed agents implicitly trust each other without cryptographic validation, identity attestation, and contextual authorization.

---

## 1. STRIDE Threat Analysis Matrix

| STRIDE Category | Threat Description | Attack Vector | SentinelLAN Countermeasure & Architectural Control | Residual Hardening |
|---|---|---|---|---|
| **Spoofing** (Identity) | Attacker mimics a genuine agent or administrator | Rogue agent sending telemetry; stolen web session cookie | - **One-time Enrollment Token**: Single-use, hashed with expiration.<br>- **Per-Device Secret**: Dedicated symmetric secret for agent HMAC.<br>- **Session Hardening**: PBKDF2 password hashes, short-lived HttpOnly/SameSite cookies, sliding refresh tokens with reuse-family revocation. | Machine attestation (TPM 2.0 / Ed25519 hardware keys). |
| **Tampering** (Integrity) | Attacker alters commands in transit or modifies local audit trails | Man-in-the-Middle (MITM) altering command payload; local DB editing | - **Signed Command Payloads**: Commands carry Server HMAC/Signature covering `(CommandId, DeviceId, Action, Nonce, ExpiryUtc)`.<br>- **Append-Only Audit**: Write-only ledger; no UPDATE or DELETE queries permitted on audit tables.<br>- **TLS 1.3 Termination**: Encrypted transport with certificate validation/pinning. | SQLite database encryption on agent via Windows DPAPI. |
| **Repudiation** (Denial) | Admin denies issuing a destructive command, or Agent denies receiving it | Malicious actor locks workstation and deletes traces | - **Mandatory Justification**: Every sensitive command requires an immutable `Reason` string.<br>- **Actor & Target Attribution**: Audit trail binds command ID, user ID, tenant ID, device ID, and UTC timestamp.<br>- **Agent Execution Feedback**: Two-phase acknowledgment with idempotency keys. | Off-site immutable WORM (Write Once Read Many) log shipping. |
| **Information Disclosure** | Data breach of employee activity or cross-tenant data leakage | Inquisitive admin snooping; BOLA (Broken Object Level Authorization) | - **Data Minimization (GDPR & Decree 13/2023/NĐ-CP)**: Strictly technical metrics only (CPU, RAM, Disk, Uptime). Zero screen captures, keyloggers, file browsing, or browser history.<br>- **Tenant Isolation**: Global Query Filters in EF Core (`d.OrganizationId == currentTenant`) guarantee database-level isolation.<br>- **Employee Transparency**: `/my-device` portal allows employees to inspect all tracked data. | Column-level encryption for sensitive configuration items. |
| **Denial of Service** | DDoS on backend or Thundering Herd on agent reconnect | Thousands of agents hammering API after network outage; public port flood | - **Exponential Backoff & Jitter**: Reconnecting agents randomize retry intervals (2s, 4s, 8s... + jitter) to eliminate thundering herd.<br>- **ASP.NET Core Rate Limiting**: Token-bucket rate limiting per IP and device credential.<br>- **Cloudflare Edge Protection**: Ingress reverse proxy filters Layer 3/4 and HTTP floods. | Distributed Redis backplane for clustered SignalR scale-out. |
| **Elevation of Privilege** | Attacker uses agent as a web shell or bypasses technician permissions | Sending arbitrary terminal commands (`cmd.exe`, `bash`) | - **Strict Command Allow-List**: Zero support for arbitrary shell execution. Only pre-compiled actions (`ShowNotification`, `SimulateLock`, `RestartService`) are recognized.<br>- **RBAC Authorization Policies**: `RequireRole("Admin")` for critical policy/command creation.<br>- **Two-Man Rule (Future)**: Dual authorization requirement for critical system reboots. | OS-level least-privileged service account with sandboxing. |

---

## 2. Zero Trust Core Tenets (NIST SP 800-207)

1. **Continuous Verification**: All resource access (API endpoints, SignalR hubs, command dispatch) requires dynamic policy checks on every single request. No device is trusted solely because it resides within the physical office LAN.
2. **Limit the Blast Radius (Least Privilege)**:
   - Technicians can only monitor health and triage assigned alerts.
   - Employees have read-only access strictly to their assigned hardware asset via `/my-device`.
   - Agents possess no credentials to query other devices or administrative endpoints.
3. **Assume Breach**:
   - Commands require cryptographic signatures, valid nonces, and tight expiration windows (e.g., 30–60 seconds).
   - Even if an attacker compromises network traffic, replaying an intercepted command packet will be rejected by the agent due to nonce tracking and timestamp expiry.

---

## 3. Regulatory & Ethical Compliance

- **Vietnam Decree 13/2023/NĐ-CP (Personal Data Protection)** & **EU GDPR**:
  SentinelLAN fundamentally distinguishes itself from intrusive employee monitoring software (Bossware). By strictly outlawing keylogging, audio/video monitoring, screen captures, and arbitrary filesystem inspection, SentinelLAN satisfies privacy-by-design standards. The **Employee Transparency View** empowers the end user to verify their privacy rights in real time.

