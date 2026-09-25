# Privacy and telemetry disclosure

Only authorized technical endpoint telemetry is collected:

- UTC server heartbeat time and online/offline status (offline after two minutes; notification scan every five seconds).
- System CPU utilization sampled between readings, physical RAM utilization, and the system/root volume's used disk percentage.
- Declared device name, OS description, and Agent version.

On Windows, CPU uses GetSystemTimes and RAM uses GlobalMemoryStatusEx. On Linux, the collector reads aggregate /proc/stat and MemTotal/MemAvailable from /proc/meminfo. Windows hosts with more than 64 logical processors require additional processor-group support; Linux container/cgroup-specific accounting is not implemented.

Telemetry does not contain keystrokes, screen content, personal file contents, browsing activity, audio/video, network payloads, or authentication secrets. Credentials are used separately for authentication; the server stores hashes. The Development Agent identity file is not yet protected by the OS.

In Production, the Windows Agent encrypts identity with DPAPI; the Linux Agent uses a unique operator-managed key with AES-GCM and restrictive file permissions. The Development identity file remains unprotected and must not be reused for deployment. Employee users can see their assigned device, assigned policy name (configuration only; no Agent enforcement), and related audit events. Tenant boundaries use explicit query scopes; global query filters have not been implemented. This disclosure describes implemented behavior and does not certify regulatory compliance.

References: [GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes), [GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex), [Linux proc documentation](https://docs.kernel.org/filesystems/proc.html).
