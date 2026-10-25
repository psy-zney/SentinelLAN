using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public sealed class AgentWakeHub(AgentWakeConnectionGuard guard) : Hub
{
    public static string Group(Guid org, Guid device) => $"agent:{org:N}:{device:N}";
    public override async Task OnConnectedAsync()
    {
        var agent = Context.User?.ToAgentContext();
        if (agent is null || !await guard.RegisterAsync(Context.ConnectionId, agent.Value, Context.Abort, Context.ConnectionAborted)) { Context.Abort(); return; }
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(agent.Value.OrganizationId, agent.Value.DeviceId));
        await base.OnConnectedAsync();
    }
    public override Task OnDisconnectedAsync(Exception? exception) { guard.Remove(Context.ConnectionId); return base.OnDisconnectedAsync(exception); }
}
public sealed class AgentWakeConnectionGuard(IServiceScopeFactory scopes)
{
    private readonly object registrationGate = new();
    private readonly ConcurrentDictionary<string, (AgentContext Agent, Action Abort)> connections = new();
    public async Task<bool> RegisterAsync(string id, AgentContext agent, Action abort, CancellationToken ct)
    {
        if (!await ActiveAsync(agent, ct)) return false;
        // Limit stale/reconnecting clients without letting callers choose any group.
        lock (registrationGate)
        {
            if (connections.Values.Count(x => x.Agent == agent) >= 2) return false;
            return connections.TryAdd(id, (agent, abort));
        }
    }
    public void Remove(string id) => connections.TryRemove(id, out _);
    public void AbortAll() { foreach (var connection in connections.ToArray()) if (connections.TryRemove(connection.Key, out var removed)) removed.Abort(); }
    public async Task RevalidateAsync(CancellationToken ct)
    {
        foreach (var connection in connections.ToArray())
            if (!await ActiveAsync(connection.Value.Agent, ct) && connections.TryRemove(connection.Key, out var removed)) removed.Abort();
    }
    private async Task<bool> ActiveAsync(AgentContext agent, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        return await (from credential in db.DeviceCredentials.AsNoTracking()
                      join device in db.Devices.AsNoTracking() on credential.DeviceId equals device.Id
                      join organization in db.Organizations.AsNoTracking() on device.OrganizationId equals organization.Id
                      where credential.OrganizationId == agent.OrganizationId && device.OrganizationId == agent.OrganizationId && device.Id == agent.DeviceId &&
                          credential.RevokedAt == null && credential.RequestSigningConfirmedAt != null && !device.IsRevoked && !organization.IsSuspended
                      select device.Id).AnyAsync(ct);
    }
}
