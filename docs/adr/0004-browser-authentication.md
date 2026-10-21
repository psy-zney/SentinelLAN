# ADR 0004: Browser cookie sessions and rotating refresh tokens

Status: Accepted — 2026-08-28

## Decision

Use a short signed access token and an opaque rotating refresh token in host-only HttpOnly cookies. Persist only refresh-token hashes and revoke the whole token family when a rotated token is reused. Protect state-changing browser requests with a custom anti-CSRF header and an explicit credentialed-origin allow-list.

Identify the tenant during login with a unique organization code. Enforce role permissions through ASP.NET Core policies and data access through Application-owned tenant/assignment scopes. Authenticate SignalR with the same access cookie and isolate connections by organization group. Keep Agent credentials on their separate authentication path.

## Consequences

- Browser JavaScript and storage cannot read either token.
- Refresh rotation survives API restarts because session state is in PostgreSQL.
- Login requests require `organizationCode`, and deployments must configure TLS, allowed web origins and a strong access-token signing key.
- Access-token theft remains effective until its 15-minute expiry; MFA and managed signing-key rotation remain future work.
