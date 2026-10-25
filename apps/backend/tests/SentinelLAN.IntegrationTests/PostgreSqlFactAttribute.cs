namespace SentinelLAN.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__SentinelLAN")))
            Skip = "Requires the isolated PostgreSQL schema fixture configured by ConnectionStrings__SentinelLAN.";
    }
}
