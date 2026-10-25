using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SentinelLAN.Application;

namespace SentinelLAN.Api;

public sealed class RealtimeConnectionGuard(IServiceScopeFactory scopes, IHubContext<UpdatesHub> hub, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Connection> _connections = new();
    private readonly object registrationGate = new();

    public bool Register(string connectionId, ActorContext actor, Action abort)
    {
        lock (registrationGate)
        {
            if (_connections.Values.Count(x => x.Actor.OrganizationId == actor.OrganizationId && x.Actor.UserId == actor.UserId) >= 8) return false;
            return _connections.TryAdd(connectionId, new(actor, abort));
        }
    }
    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public async Task RevalidateAsync(CancellationToken cancellationToken)
    {
        if (_connections.IsEmpty) return;
        await using var scope = scopes.CreateAsyncScope();
        var validator = scope.ServiceProvider.GetRequiredService<IRealtimeSessionValidator>();
        foreach (var (id, connection) in _connections.ToArray())
        {
            var actor = connection.Actor;
            if (await validator.IsActiveAsync(actor, clock.GetUtcNow(), cancellationToken)) continue;
            if (!_connections.TryRemove(id, out _)) continue;
            connection.Abort();
            await hub.Groups.RemoveFromGroupAsync(id, TenantGroup.Name(actor.OrganizationId), cancellationToken);
        }
    }

    public void AbortAll()
    {
        foreach (var id in _connections.Keys)
            if (_connections.TryRemove(id, out var connection)) connection.Abort();
    }

    private sealed record Connection(ActorContext Actor, Action Abort);
}
