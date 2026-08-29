using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SentinelLAN.Api;

[Authorize]
public sealed class UpdatesHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var actor = Context.User?.ToActorContext();
        if (actor is not null) await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup.Name(actor.Value.OrganizationId));
        await base.OnConnectedAsync();
    }
}

public static class TenantGroup
{
    public static string Name(Guid organizationId) => $"organization:{organizationId:N}";
}
