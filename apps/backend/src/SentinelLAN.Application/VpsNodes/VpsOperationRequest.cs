using System.Text.RegularExpressions;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed record VpsOperationRequest(string Action, string? Target, string? Value, string Reason, bool Confirmed, Guid Nonce, DateTimeOffset ExpiresAt)
{
    public bool HasAllowedCommand() => Action switch
    {
        "Reboot" => Target is null && Value is null,
        "EnableServiceStartup" or "DisableServiceStartup" => Target is not null && VpsNode.GetAllowedServices().Contains(Target) && Value is null,
        "SetContainerRestartPolicy" => Target is not null && Regex.IsMatch(Target, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant) &&
            Value is "no" or "always" or "unless-stopped",
        _ => false
    };
}
