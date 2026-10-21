# ADR 0001: Technology stack

Status: Accepted — 2026-08-17

The initial stack uses .NET 10, ASP.NET Core/EF Core with Npgsql, Node/npm, Next.js, React, TypeScript, Tailwind, PostgreSQL and OpenAPI generation. The exact versions are pinned in `global.json`, package lockfiles and container definitions; use those files as the current source of truth. Redis was considered during design but is not part of the running stack. Dependency-audit and Docker results are point-in-time evidence and must be rerun for the commit being deployed.
