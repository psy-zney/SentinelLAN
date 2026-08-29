# Browser authentication and tenant authorization

Human users authenticate with an organization code, normalized email and password. The demo organization code is `demo`. Passwords are hashed with PBKDF2-SHA256 using a random salt and 210,000 iterations; successful use of a legacy SHA-256 demo hash upgrades it in place.

The API issues a 15-minute signed access token and a random 7-day refresh token as host-only, HttpOnly, `SameSite=Strict` cookies. Cookies use `Secure` on HTTPS requests, so production deployment must terminate TLS correctly. The browser never receives token values in JSON and does not use local or session storage for authentication.

Refresh tokens are stored only as SHA-256 hashes. Every refresh rotates the token in one transaction and links the replacement to a token family. Reuse of a rotated token revokes every active session in that family. Logout revokes the presented refresh session and expires both cookies.

State-changing cookie-authenticated requests require the `X-SentinelLAN-CSRF: 1` header. CORS accepts credentials only from origins listed in `SENTINELLAN_WEB_ORIGINS`. Use an unpredictable `SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY` of at least 32 characters; the API refuses the development fallback outside the Development environment.

ASP.NET Core policies authorize Admin, Technician and Employee roles. Application `DeviceScope` always combines `OrganizationId` with Employee assignment, so a route ID alone never grants access. SignalR connections are authenticated and updates are sent only to the actor's organization group. Agent authentication remains separate and continues to use per-device credentials.

The role-policy matrix is explicit: Admin-only administration/audit, Admin-or-Technician operations, Employee-only assigned-device transparency, and Agent-only device transport. Agent transport uses the separate `SentinelAgent` scheme with `X-SentinelLAN-Device-Id` and `X-SentinelLAN-Device-Secret`; heartbeat, poll, and result payloads do not carry credentials.
