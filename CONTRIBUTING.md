# Contributing

Use the product scope in [docs/project-overview.md](docs/project-overview.md), the module boundaries in [ADR 0002](docs/adr/0002-modular-monolith.md), and the [threat model](docs/security/threat-model.md). Open an issue or pull request describing the authorized endpoint use case, affected module, data collected, and acceptance evidence. Keep VPS management explicitly marked as an optional extension. Do not add LAN discovery, packet capture, covert surveillance, arbitrary commands, or real lock/isolation to the core workflow. Do not include device credentials, `.env` files, certificates, logs, database dumps, or generated artifacts.

Before submitting code, run `dotnet build SentinelLAN.slnx`, `dotnet test SentinelLAN.slnx`, and the relevant web checks (`npm run lint`, `npm run typecheck`, `npm test`, `npm run build`). Update OpenAPI and documentation when an external contract changes. Keep destructive device actions simulated unless a separately reviewed authorized lab adapter is added.

Report security issues privately as described in [SECURITY.md](SECURITY.md).
