# Employee self-service implementation contract

This describes the implemented employee-facing expansion. Keep Vietnamese employee copy simple. Existing device management remains intact. All paths below are relative to `/api/v1/self-service`, JSON camelCase, UUID identifiers, UTC ISO timestamps, authenticated and tenant scoped. Employee scope is the active assigned device and their own requests. IT reads require Admin/Technician; approval of privileges/maintenance/catalog publishing requires Admin. Sensitive writes carry a reason and confirmation; creation and chat use idempotency keys. Never send OTPs in push notifications or audit logs.

## Shared DTOs

`SupportRequest`: id, deviceId, deviceName, userId, userName, kind, category, title, description, canWork, status, assignedTechnicianId (nullable), assignedTechnicianName (nullable), catalogAppId (nullable), commandId (nullable), commandStatus (nullable), commandMessage (nullable), appointmentAt (nullable), createdAt, updatedAt, approvalExpiresAt (nullable).

Kinds: `Incident`, `InstallApp`, `Privilege`, `Panic`, `PauseAgent`, `UninstallAgent`, `Appointment`. Statuses: `Open`, `InProgress`, `AwaitingEmployee`, `Approved`, `Rejected`, `Resolved`, `Closed`. Categories: `Network`, `Printer`, `Slow`, `Application`, `Suspicious`, `Other`. Command status is separate from approval; approved is never proof of execution.

`SupportMessage`: id, requestId, authorId, authorName, body, createdAt.
`SupportAttachment`: id, requestId, fileName, contentType, size, createdAt. Download bytes only through authenticated endpoint; images JPEG/PNG only, <= 2 MiB, validate magic bytes; never HTML/SVG, no shared public URLs.
`CatalogApp`: id, name, description, version, packageUrl, sha256, publisherThumbprint, isActive, requiresApproval, createdAt. Package URL HTTPS only, MSI only, no arbitrary arguments or scripts. Agent independently enforces configured trusted package hosts, hash and Authenticode publisher identity.
`Announcement`: id, title, body, isOutage, requiresAcknowledgement, startsAt, endsAt, acknowledged, affected, createdAt.
`EmployeeNotification`: id, title, body, requestId (nullable), readAt (nullable), createdAt. Push bodies are generic; details remain authenticated.
`HelpArticle`: id, category, title, steps (string array).

## Routes

- GET `/requests`: employee's own requests or IT tenant queue. POST `/requests`: {kind, category, title, description?, canWork, catalogAppId?, appointmentAt?, confirmed, idempotencyKey}; derive target device from assignment for Employee.
- GET `/requests/{id}`; GET/POST `/requests/{id}/messages` (POST {body,idempotencyKey}). Poll active conversations every 5 seconds, with visibility/background handling.
- GET/POST `/requests/{id}/attachments`: POST JSON {fileName,contentType,base64}; GET list; GET `/requests/{id}/attachments/{attachmentId}` returns bytes. Explicit employee preview/consent.
- PATCH `/requests/{id}` IT: {status, assignedTechnicianId?, reason, confirmed}. Employee PATCH only `Closed` (worked) or `Open` (reopen), reason+confirmed.
- POST `/requests/{id}/decision`: Admin {approved,reason,confirmed}; response {request,otp?,expiresAt?}. Maintenance OTP is returned once only here to approving Admin, 8 cryptographic decimal digits, 5-minute expiry, hash-only persistence, max 5 attempts, device/user/action bound, atomic consumption. Install/Privilege approval queues signed `InstallApprovedApp`; an approved installer operation is the bounded alternative to granting local administrator membership. Approval window <= 30 minutes.
- POST `/requests/{id}/redeem`: Employee {code,confirmed}; maintenance only, queues signed `PauseAgent` or `UninstallAgent`, returns SupportRequest. Backend also offers authenticated Agent POST `/api/v1/agent/self-service/maintenance/redeem`: {requestId,code}, same assignment/action/expiry/single-use checks and signed command queue.
- GET `/catalog`; POST `/catalog` Admin: {name,description,version,packageUrl,sha256,publisherThumbprint,isActive,requiresApproval,reason,confirmed}; PATCH `/catalog/{id}` same body, keeps approval package snapshot immutable.
- GET `/announcements`; POST `/announcements` IT: {title,body,isOutage,requiresAcknowledgement,startsAt,endsAt,reason,confirmed}; POST `/announcements/{id}/acknowledge`; POST `/announcements/{id}/affected` idempotent for the current user.
- GET `/notifications`; POST `/notifications/{id}/read`; POST `/push-devices`: {token,platform}; DELETE `/push-devices`: {token}. Persist authenticated recipient binding, deduplicate, rotate/remove on signout; configurable Expo push delivery worker with retries, generic bodies, no notification considered read from send receipt.
- GET `/help`: seeded short Vietnamese troubleshooting steps. POST requests is escalation from an article.

## Agent and maintenance

`InstallApprovedApp` parameter JSON: {requestId,packageUrl,sha256,publisherThumbprint,approvalExpiresAt}; signed by existing signer. `PauseAgent`/`UninstallAgent` parameter JSON: {requestId,maintenanceExpiresAt,pauseMinutes:15}. These new commands are forbidden through the generic admin command endpoint to prevent bypassing approval/OTP. Agent allowlists explicitly, validates expiry and existing nonce/signature checks. Real installation and maintenance are explicit local opt-ins, least privilege, configured package host allowlist. Never arbitrary shell. Pause state is persisted/protected and automatically expires; continue control/health heartbeat with explicit maintenance state rather than disabling service recovery. Approved uninstall invokes only the installed SentinelLAN MSI product code via Windows Installer, never an arbitrary path. A signed maintenance authorization must be persisted before the external installer runs; no success claim until observed completion. Recovery is disabled only for an approved uninstall.

Task Manager forced termination cannot show an OTP prompt. Standard-user service/process ACLs deny unapproved stops. An explicit desktop maintenance popup uses an ACL-protected pipe to redeem a device-bound approval code through the Agent; it does not expose device credentials. IT retains an administrative recovery/uninstall path. Offline heartbeat notices are labelled loss of connectivity, never proof of deliberate termination. Approved MSI installation requires an explicitly elevated Agent deployment plus `SENTINELLAN_ALLOW_APPROVED_APP_INSTALL=true` and a trusted package-host allowlist. The default LocalService deployment cannot install MSI or silently grant full local administrator membership. Agent uninstall requires the same explicit elevated deployment and `SENTINELLAN_ALLOW_AGENT_MAINTENANCE=true`; verify actual MSI removal on a lab device before promising this flow to employees.

## Panic and notifications

Panic creates a critical support request and IT alert for the assigned device, queues `IsolateNetwork` through an Application command port only when server lab configuration explicitly permits the device; otherwise records a clear unavailable outcome and still reports the incident. Retain current local Agent lab restrictions and recovery interval. No claim of production ransomware containment. Scheduled worker emits deduplicated employee/IT offline notices and reconciles command outcomes to support requests; suppress expected maintenance/revocation offline alerts.

## Acceptance and validation

Test authorization/tenant/assignment, idempotency conflicts, OTP wrong/expired/replayed/concurrent usage, privileged approval, immutable package snapshot, invalid URLs/images/oversize, user-specific messages/attachments, push token ownership, outage acknowledgements, appointment bounds, confirmed close/reopen, offline dedupe, command status truthfulness, and Agent opt-in/hash/publisher/expiry checks. Add an EF migration and update OpenAPI/ADR/privacy documentation. Run backend/Agent tests, web/mobile lint/typecheck/tests and web build. Native push/camera and real Windows installation/uninstall need explicit lab-device acceptance; do not execute them on the development machine.
