# SentinelLAN repository guide

SentinelLAN is an authorized LAN endpoint-management, monitoring, and protection system. Its MVP includes the ASP.NET Core modular monolith, Next.js dashboard, PostgreSQL, and a least-privileged Windows-capable Agent. Collect only technical telemetry; never add covert surveillance, credential capture, arbitrary execution, or destructive device behavior.

## Boundaries

- Employee interfaces serve ordinary office staff with limited computer knowledge. Use plain Vietnamese, task-oriented actions, automatic device selection, explicit progress and outcomes; ask whether work is interrupted instead of asking employees to classify technical severity. Screenshots/images are employee-selected, previewed and consented, never captured covertly.

- `Domain` contains business rules and has no infrastructure dependency.
- `Application` depends only on Domain and owns use cases, contracts, validation, and authorization requirements.
- `Infrastructure` implements Application ports with EF Core, PostgreSQL, security, and integrations.
- `Api` is the composition root and transport boundary.
- Agent Core is platform-independent; Agent Infrastructure contains OS and HTTP adapters; the Worker composes them.
- Modules communicate through Application contracts/events. Never query another module's tables directly.

## Commands

- Restore/build/test: `dotnet restore SentinelLAN.slnx`, `dotnet build SentinelLAN.slnx`, `dotnet test SentinelLAN.slnx`.
- Format: `dotnet format SentinelLAN.slnx`; web: `npm.cmd ci`, `npm.cmd run lint`, `npm.cmd run typecheck`, `npm.cmd test`, `npm.cmd run build`.
- Development: `docker compose up --build`; seed: `./scripts/seed-demo.ps1` or `./scripts/seed-demo.sh`.
- Full verification: `./scripts/test-all.ps1` or `./scripts/test-all.sh`.

Use nullable C#, async APIs, UTC timestamps, immutable request records, explicit cancellation, and one public type per file when practical. TypeScript is strict; keep remote access in the typed API client and components accessible. Add a new migration instead of editing a merged migration. Run relevant tests after every change and update OpenAPI, ADRs, and docs when contracts change.

Secrets come only from environment variables or user-secrets. Never commit secrets, `.env`, build artifacts, logs, database dumps, device credentials, or certificates. Sensitive actions require permission policies, tenant checks, reason, expiry, nonce, confirmation, and append-only audit. Real lock/isolation remains disabled unless an explicit lab flag is enabled on an authorized test device.
