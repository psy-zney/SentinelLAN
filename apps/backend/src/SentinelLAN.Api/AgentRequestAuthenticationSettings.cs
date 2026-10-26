using System.Globalization;

namespace SentinelLAN.Api;

public sealed record AgentRequestAuthenticationSettings(bool DevelopmentLegacyEnabled, DateTimeOffset? LegacyUntil)
{
    public static AgentRequestAuthenticationSettings FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        DateTimeOffset? until = null;
        if (configuration["SENTINELLAN_AGENT_LEGACY_AUTH_UNTIL"] is { Length: > 0 } value)
        {
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) || parsed > DateTimeOffset.UtcNow.AddDays(7))
                throw new InvalidOperationException("SENTINELLAN_AGENT_LEGACY_AUTH_UNTIL must be a UTC expiry no more than seven days ahead.");
            until = parsed;
        }
        return new(environment.IsDevelopment() && !(configuration.GetValue<bool>("SENTINELLAN_AGENT_REQUIRE_SIGNED_REQUESTS") ||
            configuration.GetValue<bool>("SENTINELLAN_REQUIRE_SIGNED_AGENT_REQUESTS")), until);
    }
}
