# API

Development OpenAPI 3.1 is generated at `/openapi/v1.json`. Versioned endpoints live under `/api/v1`; health and SignalR are `/health/*` and `/hubs/updates`. Regenerate and review the contract whenever request/response records or routes change.

## Browser authentication

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/v1/auth/login` | Authenticate an organization code, email, and password; set access/refresh cookies |
| `POST` | `/api/v1/auth/refresh` | Rotate the refresh token and replace both cookies |
| `POST` | `/api/v1/auth/logout` | Revoke the refresh session and expire both cookies |

Access and refresh tokens are never returned in the JSON response. Browser clients send cookies with credentials and include `X-SentinelLAN-CSRF: 1` on state-changing requests. Access tokens expire after 15 minutes; refresh sessions expire after 7 days and are stored only as SHA-256 hashes. Reuse of a rotated refresh token revokes its whole token family.

User endpoints use ASP.NET Core authorization policies and tenant-scoped queries. Missing/invalid authentication returns `401`; a signed-in role without permission returns `403`; a device outside the actor's tenant or Employee assignment returns `404`.

## OpenAPI security metadata

The generated document defines `accessCookie`, `refreshCookie`, `csrfHeader`, `agentDeviceId`, and `agentDeviceSecret` schemes. Operation-level security requirements distinguish read-only browser requests, state-changing cookie requests, refresh/logout, and Agent-only endpoints. Integration tests parse the document and assert these requirements rather than checking route presence alone.

Agent heartbeat, command polling, and result routes authenticate with `X-SentinelLAN-Device-Id` plus `X-SentinelLAN-Device-Secret`. The secret is never accepted in a URL. Enrollment remains protected by a one-time hashed enrollment token and rate limiting.
