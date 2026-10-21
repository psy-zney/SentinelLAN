namespace SentinelLAN.Domain;

public sealed class PreventiveMaintenanceRun : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public DateOnly Day { get; init; }
}
