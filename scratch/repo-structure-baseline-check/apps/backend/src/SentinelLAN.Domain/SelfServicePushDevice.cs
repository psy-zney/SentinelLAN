namespace SentinelLAN.Domain;

public sealed class SelfServicePushDevice : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public required string Token { get; init; }
    public required string Platform { get; set; }
}
