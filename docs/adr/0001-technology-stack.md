# ADR 0001: Technology stack

Status: Accepted — 2026-08-17

Use .NET SDK 10.0.400 targeting net10.0, ASP.NET Core/EF Core 10.0.11 with Npgsql EF 10.0.3, C# 14, Node 24.18.0 LTS with npm 11.16.0, Next.js 16.2.11, React 19.2.8, TypeScript 5.9.3, Tailwind 4.3.3, PostgreSQL 18.1, optional Redis 8.2.1, and OpenAPI 3.1 generation. These are stable compatible patches resolved during preflight; application packages and images are exact-pinned. Next's vulnerable transitive PostCSS/Sharp versions are overridden to 8.5.26/0.35.0. Docker was not installed on the bootstrap machine, so Compose runtime verification is deferred to CI/a Docker host.
