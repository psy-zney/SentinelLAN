# Agent one-code setup verification — 2026-10-03

Implemented the Windows setup flow requested for company-managed devices. Admin issues one connection code containing the configured server origin, a single-use enrollment secret and expiry. The employee opens Setup.exe or MSI, pastes the code and connects without entering a URL or command. The MSI includes a configurator and Start menu shortcut; Setup.exe embeds that completed MSI and the configuration logic. No signing keys or long-lived device credentials are bundled in the connection code.

Server-side single use is preserved, including concurrency on UsedAt. Used and expired tokens return separate safe error codes. Setup waits for the first successful Agent cycle/heartbeat and removes the temporary enrollment token for successful, used, expired and connection-failure outcomes. Existing identity and IT settings are preserved.

Verification completed:

- Solution build succeeded. New C# code was formatted.
- Full .NET suite: 270 passed (74 Agent, 87 Application, 1 Architecture, 5 Domain, 103 Integration).
- Web suite: 34 passed. Shared web UI lint/typecheck, company typecheck and production build passed.
- MSI and self-contained Setup.exe built in dist/windows-x64. MSI decompilation confirmed configurator file, Start menu shortcut and final-dialog launch action.
- Embedded MSI and script SHA-256 values match the generated MSI and source script. SHA256SUMS.txt accompanies the local packages.
- Production and development Compose configuration validated with synthetic test settings. git diff --check passed.

Some initial checks failed due to sandbox restrictions on named pipes, temporary-file moves, Windows session reads and PowerShell execution; authorized runs outside the sandbox passed. WiX emitted its pre-existing ServiceConfigFailureActions warning; the configuration script also configures recovery explicitly through sc.exe. Restore retained NU1900 warnings about unavailable NuGet vulnerability metadata, so no successful dependency vulnerability audit is claimed.

The Docker daemon was unavailable, so Integration tests used the Development InMemory provider. PostgreSQL transaction behavior was not independently certified in this run. No live device installation, company enrollment or reboot was performed. Acceptance on a separate authorized Windows machine remains necessary before distribution. No deployment or public release was performed.

Operational prerequisite: set SENTINELLAN_AGENT_PUBLIC_URL to the trusted HTTPS origin reachable by employee devices. Production Compose defaults to https://PUBLIC_DOMAIN. A loopback development code works only on the API host itself.
