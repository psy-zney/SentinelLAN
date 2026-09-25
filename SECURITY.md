# Security policy

SentinelLAN is an MVP for authorized endpoint management. The current source is maintained on the `main` branch; no long term support release is promised.

## Report a vulnerability

Use GitHub's private vulnerability reporting feature for this repository. Do not open a public issue with exploit details, device credentials, private keys, or personal data. Include the affected version or commit, the trust boundary, a minimal reproduction using dummy data, and the expected and observed behavior. Maintainers will coordinate a fix and disclosure before publishing details.

For an actively exposed deployment, revoke affected credentials and follow [the incident workflow](docs/security/incident-workflow.md). Never test against a system without authorization.

## Security boundaries

Keep secrets in environment variables or user secrets. Production requires distinct command signing, access token signing, and server vault keys. Agent lock and network isolation commands remain simulations. VPS SSH routes require a privileged role, tenant scope, and host key pinning. Restart also requires an allowed service, confirmation, reason, expiry, nonce, and audit.
