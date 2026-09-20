# Containers & Node Topology
 
 ```mermaid
 flowchart TB
   subgraph Clients["Operators"]
     Browser["Web Browser (Admin / Tech / Employee)"]
   end

   subgraph CloudEdge["Edge & Ingress (VPS / Cloud Gateway)"]
     Proxy["Reverse Proxy / Cloudflare (Port 443 TLS 1.3)"]
   end

   subgraph MonolithStack["Application Backend"]
     Web["Next.js 16 Web Dashboard (:3000)"]
     API["ASP.NET Core Modular Monolith (:5000 / :8080)"]
     DB[("PostgreSQL 18")]
     Redis[("Redis 8 (Optional SignalR Backplane)")]
   end

   subgraph ManagedFleet["Managed Endpoint & Cloud Fleet"]
     WinAgent["Windows Agent (.NET Service / Worker)\n[Office PC / Laptop]"]
     LinuxAgent["Linux Agent (.NET systemd Daemon)\n[Oracle Cloud / AWS VPS]"]
   end

   Browser -->|HTTPS / WSS| Proxy
   Proxy -->|Reverse Proxy| Web
   Proxy -->|Reverse Proxy / SignalR| API

   WinAgent -->|Outbound TLS 1.3 HTTPS / WSS| Proxy
   LinuxAgent -->|Outbound TLS 1.3 HTTPS / WSS| Proxy

   Web -->|Typed REST Client| API
   API --> DB
   API -.-> Redis
 ```
 
- In development, Docker Compose provisions PostgreSQL, API, and Web.
- Managed agents are host-level background services (`Windows Service` or `Linux systemd`) and are never containerized when managing the host OS.

