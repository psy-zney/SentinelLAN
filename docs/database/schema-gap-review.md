# Week 1 schema-gap review

Reviewed: 2026-08-29. Scope: identity, tenant authorization, browser sessions, Employee assignment, and Agent identity.

| Area | Implemented evidence | Remaining gap / decision |
|---|---|---|
| Organization identity | Unique `Organization.Code` and `Organization.Name`; login resolves code + normalized email | Organization lifecycle and code rename are outside Week 1 |
| Human identity | Tenant-scoped unique email, PBKDF2 password hash, explicit Admin/Technician/Employee role | Role is currently a constrained string; database-backed role/permission membership and MFA remain post-MVP work |
| Browser session | Hashed rotating `RefreshSession`, family-reuse revocation, optimistic concurrency, UTC expiry | Managed signing-key rotation and session administration UI remain future work |
| Employee assignment | `Device.AssignedUserId`, organization + assignment scope, dedicated read-only `/api/v1/my-device` projection | Enforce a formal one-user/one-primary-device rule only if product requirements adopt it; current schema can assign multiple devices |
| Agent identity | One credential per device, hash-only storage, revocation timestamp, separate ASP.NET Core authentication scheme/policy | Production OS-protected credential storage and asymmetric device/command identity are Week 5 work |
| Tenant enforcement | `OrganizationId` indexes and Application-owned `DeviceScope`; endpoint tests cover cross-tenant and cross-assignment access | No EF global query filter by design; every new module must add an Application scope and negative integration tests |
| Audit integrity | Sensitive command lifecycle records actor/device/reason/outcome | Database-level append-only privileges/external immutable sink remain deployment work |
| PostgreSQL proof | CI passes `ConnectionStrings__SentinelLAN` into the API test factory; the provider assertion requires Npgsql when configured | Local runtime proof is pending on this workstation because Docker/PostgreSQL is unavailable |

No Week 1 model change was required by this review, so no migration was created. Existing merged migrations were not edited.
