# ADR 0001: Technology stack

Status: Accepted — 2026-08-17

Use .NET SDK 10.0.400 targeting net10.0, ASP.NET Core/EF Core 10.0.11 with Npgsql EF 10.0.3, C# 14, Node 24.18.0 LTS with npm 11.16.0, Next.js 16.3.5, React 19.2.8, TypeScript 5.9.3, Tailwind 4.3.3, PostgreSQL 18.1, optional Redis 8.2.1, and OpenAPI 3.1 generation. These are stable compatible patches resolved during preflight; application packages and images are exact-pinned. Next.js was updated from 16.2.11 to 16.3.5 after the 2026-09-22 dependency audit reported production advisories in Next.js and transitive Sharp; `npm audit --omit=dev` is clean at this decision revision. Docker was not installed on the bootstrap machine, so Compose runtime verification is deferred to CI/a Docker host.
