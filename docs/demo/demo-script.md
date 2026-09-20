# High-Impact 5-Minute Demonstration Runbook

This demonstration showcases SentinelLAN as a production-grade **Zero Trust Endpoint & Cloud Node Governance Platform**. It is tailored for academic committees and technical evaluations to prove architectural rigor, real-world utility, and security depth.

---

## 1. Demo Topology & Pre-Flight Setup
- **Screen 1 (Main Projector)**: Next.js 16 Web Dashboard (`http://localhost:3000`).
- **Screen 2 (Physical Laptop / Test Rig)**: Windows 11 machine running `SentinelLAN.Agent.Worker` (configured in authorized lab mode with `AllowRealExecution: true`).
- **Screen 3 (Cloud VPS / Remote Container)**: Linux Ubuntu node running SentinelLAN systemd agent (Oracle Cloud / AWS VPS) hosting an Nginx web service.

---

## 2. The 5-Minute Presentation Flow

### Act 1: Centralized Governance & Multi-Platform Telemetry (0:00 – 1:30)
1. **Sign in as Admin**:
   - Access the dashboard with tenant-isolated credentials.
   - Show the device fleet: Both the **Windows Laptop** and the **Ubuntu Cloud Node** are online.
2. **Real-time SignalR Telemetry**:
   - Show live CPU, RAM, and Disk telemetry streaming via WebSocket.
   - Highlight that the Cloud Node is communicating **Outbound-only over TLS 1.3 Port 443**—no open inbound SSH port (Port 22) is needed on the cloud security group.
   - Show multi-tenant isolation: An administrator from Tenant B cannot observe or query devices from Tenant A.

### Act 2: Live Security Defense & Attack Simulation (1:30 – 2:45)
1. **The Replay Attack**:
   - Open Postman or terminal curl. Intercept a previously valid command payload.
   - Attempt to replay the payload against `/api/v1/devices/{id}/commands`.
   - **Result**: Backend detects a duplicate `Nonce` and expired timestamp; returns `400 Bad Request / Replay Attack Detected`.
2. **The Forged Command Attack**:
   - Attempt to modify the command payload to target another device or bypass the allow-list.
   - **Result**: Signature verification fails immediately; the event is recorded in the append-only `AuditLog` as a high-severity security alert.

### Act 3: Safe Real-World Actions & Service Recovery (2:45 – 4:00)
1. **Workstation Intervention (Windows Laptop)**:
   - On the Web Dashboard, select the test laptop and dispatch the allow-listed `SimulateLock` (or `LockWorkstation` in lab mode) with justification: *"Suspected unauthorized physical access"*.
   - **Immediate Result**: Within 1 second, the laptop screen locks instantly via Win32 P/Invoke `LockWorkStation()`.
2. **Cloud Node Self-Healing / Service Recovery (Linux VPS)**:
   - Open a browser tab pointing to the test website on the VPS. Simulate a crash (stop Nginx, yielding `502 Bad Gateway`).
   - On SentinelLAN Dashboard, select the VPS node and click `RestartService` (`ServiceName: nginx`).
   - **Immediate Result**: Agent executes the allow-listed systemd unit restart; the website reloads and comes back online instantly.

### Act 4: Employee Transparency & Privacy Compliance (4:00 – 4:45)
1. **Sign in as Employee (`employee@demo.sentinellan.local`)**:
   - Navigate to `/my-device`.
   - Show the **Transparency Statement**: Employee sees exactly what metrics the company gathers (CPU/RAM/Disk).
   - Show proof of **Non-Invasive Privacy**: Zero keystroke logs, zero screen captures, zero browsing history tracking.
   - Directly reference compliance with **Vietnam Decree 13/2023/NĐ-CP** and **EU GDPR**.

### Act 5: Non-Repudiation Audit Ledger (4:45 – 5:00)
1. **Return to Audit Dashboard**:
   - Show the tamper-evident, append-only log entry for the lock command and service restart.
   - Highlight: Actor (`admin`), Target (`laptop-01`), Action (`LockWorkstation`), Timestamp (UTC), Justification Reason, Nonce, and Agent Execution Status (`Success`).

---

## 3. Key Talking Points for Committee Q&A
- **Q: Why not allow freeform remote bash/PowerShell scripts?**
  - *Answer*: Freeform execution turns the management agent into a remote web shell. If the admin portal is compromised, the entire organization is breached. SentinelLAN enforces strict allow-lists with cryptographic signing and parameter boundaries.
- **Q: How does this scale in high-traffic reconnections?**
  - *Answer*: Resilient reconnect loops utilize Exponential Backoff with randomized Jitter and idempotent delivery keys, preventing the "Thundering Herd" problem on the API and PostgreSQL.

