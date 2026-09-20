# Architecture Components
 
 SentinelLAN uses a Modular Monolith backend and a decoupled Clean Architecture Agent runtime.
 
 ## 1. Backend Composition (.NET 10 Modular Monolith)
 
 The backend is structured around domain-driven bounded contexts with strict dependency boundaries:
 - **IdentityAccess**: Authentication, cookie/token issuance, session management, RBAC, and tenant membership.
 - **Organizations**: Multi-tenant boundaries, organization settings, and enterprise configuration.
 - **Devices**: Endpoint registration, enrollment token generation, cryptographic identity binding (`DeviceSecret`), and inventory management.
 - **Telemetry**: Ingestion of technical health metrics (CPU, RAM, Disk, Uptime), deduplication via idempotency keys, and offline queue recovery.
 - **Policies**: Definition, validation, and assignment of compliance rules (working hours, telemetry thresholds).
 - **Commands**: Allow-listed command dispatch, cryptographic signing (HMAC-SHA256 / Ed25519), nonce tracking, expiry enforcement, and execution state machines.
 - **Alerts**: Real-time incident detection, threshold violations, and technician triage workflows.
 - **Audit**: Append-only, immutable tamper-evident activity ledger recording all actor and agent actions.
 
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
 
 - **SentinelLAN.Agent.Core**: Platform-agnostic domain logic. Manages device identity, enrollment handshake, heartbeat scheduling with jitter, offline SQLite queuing, command signature verification, and nonce replay validation.
 - **SentinelLAN.Agent.Infrastructure**: OS and transport adapters.
   - *HTTP/SignalR Client*: Resilient connection manager with exponential backoff and jitter.
   - *Windows Adapter*: WMI/CIM hardware telemetry, DPAPI secret storage, and Win32 P/Invoke (`LockWorkStation`).
   - *Linux Adapter*: Linux `/proc` filesystem telemetry (`/proc/loadavg`, `/proc/meminfo`, `/proc/uptime`), protected filesystem secret store, and `systemd` service control (`systemctl restart`).
 - **SentinelLAN.Agent.Worker**: Host entry point composing Core and Infrastructure. Supports hosting as a Windows Service (`builder.Services.AddWindowsService()`) or a Linux systemd daemon (`builder.Services.AddSystemd()`).
 
 ### Session 0 Isolation & Enterprise Topology
 - **MVP / Lab Architecture**: The Agent runs as a background Worker / Service in Session 0. It executes allowed background actions and simulates or safely triggers desktop locks.
 - **Production Architecture**: Windows enforces Session 0 Isolation (services cannot display GUI elements or interact directly with logged-in user desktops). The production topology decouples the agent into:
   1. `SentinelLAN.Service`: Runs as `NT AUTHORITY\SYSTEM` in Session 0 for continuous telemetry, security monitoring, and anti-tamper enforcement.
   2. `SentinelLAN.TrayApp`: Runs in the active user session (Session 1+) displaying notification alerts, employee transparency modals, and two-way consent dialogs.
   3. Both components communicate locally via secure IPC (Named Pipes with ACLs).

