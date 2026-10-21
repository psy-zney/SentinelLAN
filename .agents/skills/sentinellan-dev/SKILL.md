---
name: sentinellan-dev
description: Build, review, test, document, or secure the SentinelLAN endpoint-management repository. Use for SentinelLAN backend, web dashboard, Windows Agent, device enrollment, telemetry, policy, command, alert, audit, demo, database, and architecture tasks; do not use for unrelated projects.
---

# SentinelLAN development workflow

1. Read `AGENTS.md` and the relevant architecture, security, API, and use-case documents.
2. Identify the owning module, tenant boundary, affected contracts, and dependency direction.
3. Read `references/domain-model.md` for entities, `references/security-rules.md` for trust boundaries, and `references/verification.md` for required checks when applicable.
4. Add or update tests before or alongside production code, including authorization and failure paths.
5. Make the smallest cohesive change that completes the use case; preserve safe simulated command behavior by default.
6. Run format, focused tests, build, lint/typecheck, and relevant dependency/security checks.
7. Update OpenAPI, migrations, ADRs, and documentation whenever an external interface or architectural decision changes.
8. Report changed files, verification commands and results, simulated versus real behavior, and remaining risks.
