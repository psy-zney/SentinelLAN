# System Context & Operational Scope

SentinelLAN is an authorized, privacy-first endpoint and cloud node governance platform built on **Zero Trust principles (NIST SP 800-207)** and **Privacy-by-Design**.

It serves a dual operational scope:
1. **Workstation Governance (Windows Endpoints)**: Hardware asset tracking, non-invasive health telemetry, policy enforcement, and authorized emergency intervention (e.g., workstation lock) for office and hybrid remote employees.
2. **Cloud Infrastructure Governance (Linux VPS Nodes)**: Centralized "single pane of glass" monitoring and allow-listed service recovery (e.g., restarting Nginx or Docker services) across multi-cloud providers (Oracle Cloud, AWS, GCP) without requiring open inbound SSH/management ports.

```mermaid
flowchart TD
  subgraph Operators["Human Actors"]
    Admin["IT Administrator"]
    Tech["Support Technician"]
    Employee["Authorized Employee"]
  end

  subgraph Presentation["Edge & Delivery Layer"]
    CDN["Cloudflare / Reverse Proxy (Port 443 TLS 1.3)"]
    Web["SentinelLAN Web Dashboard (Next.js 16)"]
  end

  subgraph Core["Central Management Cluster"]
    API["SentinelLAN API & SignalR Hub (.NET 10 Modular Monolith)"]
    DB[("PostgreSQL 18 (Multi-Tenant Isolated)")]
    Redis[("Redis 8 (Pub/Sub & Distributed Cache - Optional)")]
  end

  subgraph ManagedNodes["Managed Fleet (Outbound-Only Connections)"]
    subgraph OfficeHybrid["Workstation Fleet"]
      WinAgent["Windows Agent (.NET Service / Worker)\n[Laptop / Office PC]"]
    end
    subgraph CloudFleet["Cloud Infrastructure Fleet"]
      LinuxAgent["Linux Agent (.NET systemd Daemon)\n[Oracle Cloud / AWS / GCP VPS]"]
    end
  end

  Admin -->|HTTPS Session| Web
  Tech -->|HTTPS Session| Web
  Employee -->|HTTPS /my-device| Web

  Web -->|HTTPS REST / WSS| CDN
  CDN -->|Proxied REST / WSS| API

  WinAgent -->|Outbound WSS / HTTPS 443| CDN
  LinuxAgent -->|Outbound WSS / HTTPS 443| CDN

  API --> DB
  API -.-> Redis
```

## Connectivity Invariants
- **Outbound-Only Architecture**: Managed agents connect outbound to the central API via TLS 1.3 (HTTPS / WebSocket). Endpoints and cloud servers operate safely behind NAT and firewalls without requiring inbound port forwarding or exposed SSH (Port 22).
- **Tenant Isolation**: Every query and realtime SignalR group is partitioned strictly by `OrganizationId`. Cross-tenant data leakage is prevented at the database and memory layer.
- **Privacy Boundary**: Employee transparency is enforced by design; no covert surveillance, screen captures, keystrokes, or arbitrary remote shell execution are supported.

