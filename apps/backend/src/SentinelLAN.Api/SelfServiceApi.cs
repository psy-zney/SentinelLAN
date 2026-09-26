using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public static class SelfServiceApi
{
    public static IServiceCollection AddSelfService(this IServiceCollection services, IConfiguration configuration, string serverVaultKey)
    {
        services.AddScoped<ISelfServiceStore, SelfServiceStore>();
        services.AddScoped<ISelfServiceDirectory, SelfServiceDirectory>();
        services.AddScoped<ISelfServiceCommands, SelfServiceCommands>();
        services.AddScoped<ISelfServiceAudit, SelfServiceAudit>();
        services.AddScoped<SelfServiceService>();
        services.AddSingleton<IMaintenanceCodeProtector>(new MaintenanceCodeProtector(serverVaultKey));
        var labIds = (configuration["SENTINELLAN_PANIC_LAB_DEVICE_IDS"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        services.AddSingleton(new SelfServiceSettings(configuration.GetValue<bool>("SENTINELLAN_PANIC_LAB_ENABLED"), labIds));
        services.AddHostedService<SelfServiceWorker>();
        services.AddHttpClient("expo-push", client => client.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }

    public static RouteGroupBuilder MapSelfService(this RouteGroupBuilder root)
    {
        var group = root.MapGroup("/self-service")
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{Roles.Admin},{Roles.Technician},{Roles.Employee}" });
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (SelfServiceException exception) { return Results.Problem(exception.Message, statusCode: exception.StatusCode); }
        });
        group.MapGet("/requests", (HttpContext http, SelfServiceService service, CancellationToken ct) => service.GetRequestsAsync(Actor(http), ct));
        group.MapPost("/requests", async (CreateSupportRequest request, HttpContext http, SelfServiceService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(Actor(http), request, ct);
            return Results.Created($"/api/v1/self-service/requests/{created.Id:D}", created);
        }).RequireRateLimiting("sensitive");
        group.MapGet("/requests/{id:guid}", (Guid id, HttpContext http, SelfServiceService service, CancellationToken ct) => service.GetRequestAsync(Actor(http), id, ct));
        group.MapPatch("/requests/{id:guid}", (Guid id, UpdateSupportRequest request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.UpdateAsync(Actor(http), id, request, ct));
        group.MapPost("/requests/{id:guid}/decision", (Guid id, DecideSupportRequest request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.DecideAsync(Actor(http), id, request, ct)).RequireRateLimiting("sensitive");
        group.MapPost("/requests/{id:guid}/redeem", (Guid id, RedeemMaintenanceRequest request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.RedeemAsync(Actor(http), id, request, ct)).RequireRateLimiting("sensitive");
        group.MapGet("/requests/{id:guid}/messages", (Guid id, HttpContext http, SelfServiceService service, CancellationToken ct) => service.GetMessagesAsync(Actor(http), id, ct));
        group.MapPost("/requests/{id:guid}/messages", (Guid id, CreateSupportMessage request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.AddMessageAsync(Actor(http), id, request, ct)).RequireRateLimiting("sensitive");
        group.MapGet("/requests/{id:guid}/attachments", (Guid id, HttpContext http, SelfServiceService service, CancellationToken ct) => service.GetAttachmentsAsync(Actor(http), id, ct));
        group.MapPost("/requests/{id:guid}/attachments", (Guid id, CreateSupportAttachment request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.AddAttachmentAsync(Actor(http), id, request, ct)).RequireRateLimiting("sensitive");
        group.MapGet("/requests/{id:guid}/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId, HttpContext http, SelfServiceService service, CancellationToken ct) =>
        {
            var attachment = await service.DownloadAttachmentAsync(Actor(http), id, attachmentId, ct);
            http.Response.Headers.CacheControl = "private, no-store";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(attachment.Content, attachment.ContentType);
        });
        group.MapGet("/team", (HttpContext http, SelfServiceService service, CancellationToken ct) => service.GetTeamAsync(Actor(http), ct));
        group.MapGet("/catalog", (HttpContext http, SelfServiceService service, CancellationToken ct) => service.GetCatalogAsync(Actor(http), ct));
        group.MapPost("/catalog", (SaveCatalogApp request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.SaveCatalogAsync(Actor(http), null, request, ct));
        group.MapPatch("/catalog/{id:guid}", (Guid id, SaveCatalogApp request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.SaveCatalogAsync(Actor(http), id, request, ct));
        group.MapGet("/announcements", (HttpContext http, SelfServiceService service, CancellationToken ct) => service.GetAnnouncementsAsync(Actor(http), ct));
        group.MapPost("/announcements", (CreateAnnouncement request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.CreateAnnouncementAsync(Actor(http), request, ct));
        group.MapPost("/announcements/{id:guid}/acknowledge", (Guid id, HttpContext http, SelfServiceService service, CancellationToken ct) => service.ReceiptAsync(Actor(http), id, false, ct));
        group.MapPost("/announcements/{id:guid}/affected", (Guid id, HttpContext http, SelfServiceService service, CancellationToken ct) => service.ReceiptAsync(Actor(http), id, true, ct));
        group.MapGet("/notifications", (HttpContext http, SelfServiceService service, CancellationToken ct) => service.GetNotificationsAsync(Actor(http), ct));
        group.MapPost("/notifications/{id:guid}/read", (Guid id, HttpContext http, SelfServiceService service, CancellationToken ct) => service.ReadNotificationAsync(Actor(http), id, ct));
        group.MapPost("/push-devices", (RegisterPushDevice request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.RegisterPushAsync(Actor(http), request, ct)).RequireRateLimiting("sensitive");
        group.MapDelete("/push-devices", ([FromBody] DeletePushDevice request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.RemovePushAsync(Actor(http), request.Token, ct)).RequireRateLimiting("sensitive");
        group.MapGet("/help", (HttpContext http, SelfServiceService service) => service.Help(Actor(http)));

        root.MapPost("/agent/self-service/maintenance/redeem", (AgentRedeemMaintenanceRequest request, HttpContext http, SelfServiceService service, CancellationToken ct) => service.RedeemAgentAsync(http.User.ToAgentContext()!.Value, request, ct))
            .RequireAuthorization(AuthorizationPolicies.Agent).RequireRateLimiting("sensitive")
            .AddEndpointFilter(async (context, next) =>
            {
                try { return await next(context); }
                catch (SelfServiceException exception) { return Results.Problem(exception.Message, statusCode: exception.StatusCode); }
            });
        return group;
    }

    private static ActorContext Actor(HttpContext http) => http.User.ToActorContext()!.Value;
}
