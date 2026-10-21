# Verification checklist

- Format, restore, build, and focused/unit tests pass.
- Tenant isolation, permissions, token expiry/single use, command expiry/replay, result idempotency, and audit creation are tested.
- Web lint, strict typecheck, component tests, production build, and login-to-device smoke path pass.
- Compose configuration is valid and integration tests run when Docker is available.
- No secret, certificate, dump, log, generated artifact, or real destructive action is introduced.
