using SentinelLAN.Application;

namespace SentinelLAN.Api;

public static class PlatformApi
{
    public const string AccessCookie = "sentinellan.platform.access";
    public const string RefreshCookie = "sentinellan.platform.refresh";
    private const string CookiePath = "/api/v1/platform";

    public static void MapPlatform(this RouteGroupBuilder v1)
    {
        var group = v1.MapGroup("/platform").RequireRateLimiting("sensitive");
        group.MapPost("/auth/login", async (PlatformLoginRequest request, HttpContext http, AuthenticationService auth, CancellationToken ct) =>
        {
            var session = await auth.LoginPlatformAsync(request, ct);
            if (session is null) return Results.Unauthorized();
            Write(http, session);
            return Results.Ok(new CurrentSessionResponse(session.Role, session.DisplayName));
        });
        group.MapPost("/auth/refresh", async (HttpContext http, AuthenticationService auth, CancellationToken ct) =>
        {
            var result = await auth.RefreshAsync(http.Request.Cookies[RefreshCookie], ct, platform: true);
            if (result.Session is null) { Delete(http); return Results.Unauthorized(); }
            Write(http, result.Session);
            return Results.Ok(new CurrentSessionResponse(result.Session.Role, result.Session.DisplayName));
        });
        group.MapPost("/auth/logout", async (HttpContext http, AuthenticationService auth, CancellationToken ct) =>
        {
            await auth.LogoutAsync(http.Request.Cookies[RefreshCookie], ct);
            Delete(http);
            return Results.NoContent();
        });
        group.MapGet("/auth/session", () => Results.Ok(new CurrentSessionResponse(Roles.PlatformOwner, "Chủ hệ thống")))
            .RequireAuthorization(Roles.PlatformOwner);
        group.MapGet("/companies", async (HttpContext http, PlatformService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(http.User.ToActorContext()!.Value, ct))).RequireAuthorization(Roles.PlatformOwner);
        group.MapPost("/companies", async (CreateCompanyRequest request, HttpContext http, PlatformService service, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var result = await service.CreateAsync(http.User.ToActorContext()!.Value, request, ct);
            return result.Status == ManagementResultStatus.Succeeded ? Results.Ok(result.Invitation) : Error(result.Status);
        }).RequireAuthorization(Roles.PlatformOwner);
        group.MapPut("/companies/{id:guid}/status", async (Guid id, SetCompanyStatusRequest request, HttpContext http, PlatformService service, CancellationToken ct) =>
        {
            var result = await service.SetStatusAsync(http.User.ToActorContext()!.Value, id, request, ct);
            return result == ManagementResultStatus.Succeeded ? Results.NoContent() : Error(result);
        }).RequireAuthorization(Roles.PlatformOwner);
        group.MapPost("/companies/{id:guid}/invitation", async (Guid id, PlatformConfirmation request, HttpContext http, PlatformService service, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var result = await service.ReissueAsync(http.User.ToActorContext()!.Value, id, request, ct);
            return result.Status == ManagementResultStatus.Succeeded ? Results.Ok(result.Invitation) : Error(result.Status);
        }).RequireAuthorization(Roles.PlatformOwner);
        group.MapGet("/companies/{id:guid}", async (Guid id, HttpContext http, PlatformService service, CancellationToken ct) =>
        {
            var detail = await service.GetCompanyDetailsAsync(http.User.ToActorContext()!.Value, id, ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        }).RequireAuthorization(Roles.PlatformOwner);
        group.MapGet("/system/status", async (HttpContext http, PlatformService service, CancellationToken ct) =>
            Results.Ok(await service.GetSystemStatusAsync(http.User.ToActorContext()!.Value, ct))
        ).RequireAuthorization(Roles.PlatformOwner);

        // CLOUD VPS NODES (AGENTLESS SSH)
        group.MapGet("/vps-nodes", async (HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var nodes = await vpsService.GetNodesAsync(actor, ct);
            return Results.Ok(nodes);
        }).RequireAuthorization(Roles.PlatformOwner);

        group.MapGet("/vps-nodes/{id:guid}", async (Guid id, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var node = await vpsService.GetNodeByIdAsync(actor, id, ct);
            return node is null ? Results.NotFound() : Results.Ok(node);
        }).RequireAuthorization(Roles.PlatformOwner);

        group.MapPost("/vps-nodes", async (CreateVpsNodeRequest req, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var created = await vpsService.CreateNodeAsync(actor, req, ct);
            return created is null
                ? Results.BadRequest(new { detail = "Tên, host, port, username, private key hợp lệ và fingerprint SHA256 là bắt buộc." })
                : Results.Created($"/api/v1/platform/vps-nodes/{created.Id}", created);
        }).RequireAuthorization(Roles.PlatformOwner);

        group.MapDelete("/vps-nodes/{id:guid}", async (Guid id, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var deleted = await vpsService.DeleteNodeAsync(actor, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(Roles.PlatformOwner);

        group.MapPost("/vps-nodes/{id:guid}/test-connection", async (Guid id, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var result = await vpsService.TestConnectionAsync(actor, id, ct);
            return Results.Ok(result);
        }).RequireAuthorization(Roles.PlatformOwner);

        group.MapPost("/vps-nodes/{id:guid}/refresh-metrics", async (Guid id, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var node = await vpsService.RefreshMetricsAsync(actor, id, ct);
            return node is null ? Results.NotFound() : Results.Ok(node);
        }).RequireAuthorization(Roles.PlatformOwner).Produces<VpsNodeDto>().Produces(StatusCodes.Status404NotFound)
            .WithSummary("Collect a current Linux VPS, systemd and Docker resource snapshot via pinned SSH.");

        group.MapPost("/vps-nodes/{id:guid}/restart-service", async (Guid id, RestartVpsServiceRequest req, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
        {
            var actor = http.User.ToActorContext()!.Value;
            var result = await vpsService.RestartServiceAsync(actor, id, req, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireAuthorization(Roles.PlatformOwner);

        group.MapPost("/vps-nodes/{id:guid}/operations", async (Guid id, VpsOperationRequest request, HttpContext http, VpsNodeService service, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var result = await service.ExecuteOperationAsync(http.User.ToActorContext()!.Value, id, request, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireAuthorization(Roles.PlatformOwner).Produces<VpsCommandResultDto>().Produces<VpsCommandResultDto>(StatusCodes.Status400BadRequest)
            .WithSummary("Schedule reboot or configure service/container startup using confirmed, expiring, single-use commands.");
    }

    private static IResult Error(ManagementResultStatus status) => status switch
    {
        ManagementResultStatus.Forbidden => Results.Forbid(),
        ManagementResultStatus.NotFound => Results.NotFound(),
        ManagementResultStatus.Conflict => Results.Conflict(new { detail = "Công ty đã tồn tại, yêu cầu đã dùng hoặc dữ liệu vừa thay đổi. Hãy tải lại." }),
        _ => Results.BadRequest(new { detail = "Kiểm tra thông tin, lý do, xác nhận và thời hạn yêu cầu (tối đa 5 phút)." })
    };
    private static CookieOptions Options(HttpContext http, DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = http.Request.IsHttps || !http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true
    };
    private static void Write(HttpContext http, AuthenticationResult session)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Cookies.Append(AccessCookie, session.AccessToken, Options(http, session.AccessTokenExpiresAt));
        http.Response.Cookies.Append(RefreshCookie, session.RefreshToken, Options(http, session.RefreshTokenExpiresAt));
    }
    private static void Delete(HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Cookies.Delete(AccessCookie, Options(http));
        http.Response.Cookies.Delete(RefreshCookie, Options(http));
    }
}
