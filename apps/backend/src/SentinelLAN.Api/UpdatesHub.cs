using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SentinelLAN.Api;

[Authorize]
public sealed class UpdatesHub(RealtimeConnectionGuard connections) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var actor = Context.User?.ToActorContext();
        if (actor is null) { Context.Abort(); return; }
        connections.Register(Context.ConnectionId, actor.Value, Context.Abort);
        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup.Name(actor.Value.OrganizationId));
        await connections.RevalidateAsync(Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

public static class TenantGroup
{
    public static string Name(Guid organizationId) => $"organization:{organizationId:N}";
}
