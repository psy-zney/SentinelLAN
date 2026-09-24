# Architecture Components
 
 SentinelLAN uses a Modular Monolith backend and a decoupled Clean Architecture Agent runtime.
 
 ## 1. Backend Composition (.NET 10 Modular Monolith)
 
 The backend is structured around domain-driven bounded contexts with strict dependency boundaries:
 - **IdentityAccess**: Authentication, cookie/token issuance, session management, RBAC, and tenant membership.
 - **Organizations**: Multi-tenant boundaries, organization settings, and enterprise configuration.
 - **Devices**: Endpoint registration, enrollment token generation, cryptographic identity binding (`DeviceSecret`), and inventory management.
- **Telemetry**: Ingestion of technical health metrics (CPU, RAM, Disk), heartbeat deduplication via idempotency keys, and recovery from the Agent's bounded offline queue. Production persists the queue in a protected file; Development defaults to memory.
 - **Policies**: Definition, validation, and assignment of compliance rules (working hours, telemetry thresholds).
- **Commands**: Allow-listed command dispatch, HMAC-SHA256 signing, nonce tracking, expiry enforcement, and execution state machines. Production persists at most one short-lived pending result in a protected file for retry after restart. Asymmetric signing and key rotation remain future work.
 - **Alerts**: Real-time incident detection, threshold violations, and technician triage workflows.
- **Audit**: Application-level append-only guard for tracked `AuditLog` changes. It is not a database-level or externally tamper-evident ledger; WORM/SIEM integration remains future work.
 
 ```mermaid
 flowchart LR
   subgraph Monolith["Modular Monolith Boundary"]
     Api["SentinelLAN.Api\n(Composition Root, Endpoints, SignalR Hubs)"]
     App["SentinelLAN.Application\n(Use Cases, Contracts, Authorization)"]
     Domain["SentinelLAN.Domain\n(Entities, Invariants, Value Objects)"]
     Infra["SentinelLAN.Infrastructure\n(EF Core, PostgreSQL, Crypto Adapters)"]
   end

   Api --> App
   App --> Domain
   Api --> Infra
   Infra --> App
   Infra --> PostgreSQL[("PostgreSQL 18")]
 ```
 
 ## 2. Agent Architecture (.NET Cross-Platform Runtime)
 
 The SentinelLAN Agent follows Clean Architecture to ensure that the core telemetry, cryptographic verification, and state machine remain 100% platform-independent:
 
- **SentinelLAN.Agent.Core**: Platform-agnostic logic for device identity, enrollment, heartbeat delivery, bounded offline telemetry queuing, HMAC command verification, and nonce replay validation. Production composes protected file stores; Development defaults to in-memory queue and nonce stores.
 - **SentinelLAN.Agent.Infrastructure**: OS and transport adapters.
  - *HTTP Client*: Retry delay grows exponentially with a small symmetric random adjustment (currently ±500 ms); this is not full jitter. Production persists up to 50 telemetry entries for one hour in a protected file; Development uses an in-memory queue.
  - *Windows Adapter*: Win32 telemetry APIs and DPAPI secret storage. Lock behavior remains simulated in the current safe command executor.
  - *Linux Adapter*: Linux `/proc` telemetry and platform secret storage. Real device lock and network isolation remain disabled; service restart is currently simulated by the Agent command executor.
 - **SentinelLAN.Agent.Worker**: Host entry point composing Core and Infrastructure. Supports hosting as a Windows Service (`builder.Services.AddWindowsService()`) or a Linux systemd daemon (`builder.Services.AddSystemd()`).
 
 ### Session 0 Isolation & Enterprise Topology
 - **MVP / Lab Architecture**: The Agent runs as a background Worker / Service in Session 0. It executes allowed background actions and simulates or safely triggers desktop locks.
 - **Production Architecture**: Windows enforces Session 0 Isolation (services cannot display GUI elements or interact directly with logged-in user desktops). The production topology decouples the agent into:
   1. `SentinelLAN.Service`: Runs as `NT AUTHORITY\SYSTEM` in Session 0 for continuous telemetry, security monitoring, and anti-tamper enforcement.
   2. `SentinelLAN.TrayApp`: Runs in the active user session (Session 1+) displaying notification alerts, employee transparency modals, and two-way consent dialogs.
   3. Both components communicate locally via secure IPC (Named Pipes with ACLs).
