namespace SentinelLAN.Agent.Infrastructure;

public static class AgentServerConfiguration
{
    public static Uri Resolve(string? configuredUrl, string? companyUrl, bool development)
    {
        var pinned = string.IsNullOrWhiteSpace(companyUrl) ? null : Parse(companyUrl, false);
        var configured = string.IsNullOrWhiteSpace(configuredUrl) ? null : Parse(configuredUrl, development && pinned is null);
        if (pinned is not null && configured is not null && pinned != configured)
            throw new InvalidOperationException("Agent configuration does not match this company's server. Use the company installer or contact IT.");
        return pinned ?? configured ?? throw new InvalidOperationException(
            "Agent server is not configured. Run the company installer; SENTINELLAN_API_URL has no default server.");
    }

    private static Uri Parse(string value, bool allowDevelopmentLoopback)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            (!allowDevelopmentLoopback && (uri.Scheme != "https" || uri.IsLoopback)) ||
            (uri.Scheme == "http" && !uri.IsLoopback) || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            throw new InvalidOperationException("Agent server must be an HTTPS origin. Loopback is allowed only for explicitly configured development builds.");
        return uri;
    }
}
