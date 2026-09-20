# Acceptance checklist

- [ ] PostgreSQL becomes healthy and API ready check passes.
- [ ] Demo users are seeded from environment/user-secrets without logging passwords.
- [ ] Enrollment token succeeds once and fails on reuse/expiry.
- [ ] Heartbeat is idempotent and dashboard receives online status.
- [ ] Safe command is signed, expires, executes only as simulation, and reports once.
- [ ] Audit contains actor, device, reason, time, and result.
- [ ] Employee transparency view discloses collected data.
- [ ] Unit, integration, lint, typecheck, build, and smoke checks pass.
