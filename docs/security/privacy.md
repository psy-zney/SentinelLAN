# Privacy and telemetry disclosure

Only authorized technical endpoint telemetry is collected:

- UTC server heartbeat time and online/offline status (offline after two minutes; self-service notification reconciliation every 15 seconds).
- System CPU utilization sampled between readings, physical RAM utilization, and the system/root volume's used disk percentage.
- Declared device name, OS description, and Agent version.

On Windows, CPU uses GetSystemTimes and RAM uses GlobalMemoryStatusEx. On Linux, the collector reads aggregate /proc/stat and MemTotal/MemAvailable from /proc/meminfo. Windows hosts with more than 64 logical processors require additional processor-group support; Linux container/cgroup-specific accounting is not implemented.

Technical telemetry does not contain keystrokes, screen content, personal file contents, browsing activity, audio/video, network payloads, or authentication secrets. Support requests may contain text, chat messages, and JPEG/PNG images that an employee explicitly selects or photographs, previews, and sends to IT. Images are limited to 2 MiB, available only through authenticated tenant-scoped request endpoints, and never captured automatically by the Agent. Employees should avoid including passwords or personal documents in an image. Credentials are used separately for authentication; the server stores hashes. The Development Agent identity file is not yet protected by the OS.

The self-service module also stores request status, command results, appointment times, announcement acknowledgements, in-app notifications, and an opt-in mobile push token. Push text on the lock screen is generic; request details require signing in. Maintenance verification codes are stored as keyed hashes and are never included in push or audit records. Heartbeat loss may trigger a notification after the normal offline threshold; it cannot establish why the Agent stopped reporting. Support images and chat remain stored until the organization applies its retention procedure; automatic expiry is not currently implemented.

In Production, the Windows Agent encrypts identity with DPAPI; the Linux Agent uses a unique operator-managed key with AES-GCM and restrictive file permissions. The Development identity file remains unprotected and must not be reused for deployment. Employee users can see their assigned device, assigned policy name (configuration only; no Agent enforcement), and related audit events. Tenant boundaries use explicit query scopes; global query filters have not been implemented. This disclosure describes implemented behavior and does not certify regulatory compliance.

References: [GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes), [GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex), [Linux proc documentation](https://docs.kernel.org/filesystems/proc.html).
