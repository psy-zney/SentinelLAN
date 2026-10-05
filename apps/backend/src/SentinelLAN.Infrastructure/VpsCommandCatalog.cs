using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public static class VpsCommandCatalog
{
    // Only technical state is printed; environment, container commands and logs are excluded.
    public const string Probe = """
        export LC_ALL=C
        set -e
        printf 'HOST|'; uname -srm
        printf 'UPTIME|'; uptime -p
        first=$(awk '/^cpu / { total=0; for(i=2;i<=9;i++) total+=$i; printf "%.0f %.0f\n", total, $5+$6; exit }' /proc/stat)
        sleep 1
        second=$(awk '/^cpu / { total=0; for(i=2;i<=9;i++) total+=$i; printf "%.0f %.0f\n", total, $5+$6; exit }' /proc/stat)
        awk -v a="$first" -v b="$second" 'BEGIN { split(a,x); split(b,y); d=y[1]-x[1]; if(d>0) printf "CPU|%.1f\n", 100*(1-(y[2]-x[2])/d) }'
        awk '/MemTotal:/ {t=$2} /MemAvailable:/ {a=$2; found=1} END {if(t>0 && found) printf "MEM|%.1f\n", 100*(t-a)/t}' /proc/meminfo
        df -Pk / | awk 'NR==2 {gsub(/%/,"",$5); print "DISK|" $5}'
        printf 'SYSTEM|'; systemctl is-system-running 2>/dev/null || true
        for svc in nginx docker sentinellan-agent cron systemd-resolved; do
          active=$(systemctl is-active "$svc" 2>/dev/null) || true
          startup=$(systemctl is-enabled "$svc" 2>/dev/null) || true
          printf 'SERVICE|%s|%s|%s\n' "$svc" "$active" "$startup"
        done
        if ! command -v docker >/dev/null 2>&1; then
          printf 'DOCKER|missing\n'
        elif ! timeout 8s docker info --format '{{.ServerVersion}}' >/dev/null 2>&1; then
          printf 'DOCKER|unavailable\n'
        else
          printf 'DOCKER|available\n'
          if containers=$(timeout 10s docker ps -a --no-trunc --size --format '{{json .}}' 2>/dev/null); then
            if [ -n "$containers" ]; then printf '%s\n' "$containers" | sed 's/^/PS|/'; fi
          else printf 'DOCKER_ERROR|Không đọc được danh sách container.\n'; fi
          ids=$(timeout 8s docker ps -aq --no-trunc 2>/dev/null) || true
          if [ -n "$ids" ]; then
            if inspected=$(timeout 10s docker inspect --format '{"Id":{{json .Id}},"RestartPolicy":{{json .HostConfig.RestartPolicy.Name}},"Health":{{with (index .State "Health")}}{{json .Status}}{{else}}null{{end}},"Ports":{{json .NetworkSettings.Ports}},"NetworkMode":{{json .HostConfig.NetworkMode}}}' $ids 2>/dev/null); then
              printf '%s\n' "$inspected" | sed 's/^/INSPECT|/'
            else printf 'DOCKER_ERROR|Không đọc được health/restart policy.\n'; fi
            if stats=$(timeout 10s docker stats --no-stream --no-trunc --format '{{json .}}' 2>/dev/null); then
              if [ -n "$stats" ]; then printf '%s\n' "$stats" | sed 's/^/STATS|/'; fi
            else printf 'DOCKER_ERROR|Không đọc được tài nguyên container.\n'; fi
          fi
        fi
        """;

    public static string BuildOperation(VpsOperationRequest request)
    {
        if (!request.HasAllowedCommand()) throw new ArgumentException("Unsupported VPS operation.", nameof(request));
        return request.Action switch
        {
            "Reboot" => Privileged("shutdown -r +1"),
            "EnableServiceStartup" => Privileged($"systemctl enable {request.Target}") + $" && systemctl is-enabled --quiet {request.Target}",
            "DisableServiceStartup" => Privileged($"systemctl disable {request.Target}") + $" && test \"$(systemctl is-enabled {request.Target} 2>/dev/null)\" = disabled",
            "SetContainerRestartPolicy" => Privileged($"docker update --restart={request.Value} {request.Target}") +
                $" && test \"$(docker inspect --format '{{{{.HostConfig.RestartPolicy.Name}}}}' {request.Target} 2>/dev/null || sudo -n docker inspect --format '{{{{.HostConfig.RestartPolicy.Name}}}}' {request.Target})\" = {request.Value}",
            _ => throw new ArgumentException("Unsupported VPS operation.", nameof(request))
        };
    }

    public static string BuildRestart(string serviceName)
    {
        // Exact matching is required because these values become shell arguments.
        if (!VpsNode.GetAllowedServices().Contains(serviceName)) throw new ArgumentException("Unsupported service.", nameof(serviceName));
        return Privileged($"systemctl restart {serviceName}") + $" && systemctl is-active --quiet {serviceName}";
    }

    private static string Privileged(string command) => $"({command} || sudo -n {command})";
}
