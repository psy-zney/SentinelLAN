namespace SentinelLAN.Application;

public interface IRealtimeSessionValidator
{
    Task<bool> IsActiveAsync(ActorContext actor, DateTimeOffset now, CancellationToken cancellationToken);
}
