using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Api;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<AuthenticationOpenApiTransformer>();
    options.AddOperationTransformer<AuthenticationOpenApiTransformer>();
});
builder.Services.AddSignalR();
builder.Services.AddHostedService<DeviceStatusPublisher>();
var allowedOrigins = (builder.Configuration["SENTINELLAN_WEB_ORIGINS"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
        $"{context.Request.Path}|{context.User.ToAgentContext()?.DeviceId.ToString() ?? context.User.ToActorContext()?.UserId.ToString() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});

var connectionString = builder.Configuration.GetConnectionString("SentinelLAN");
builder.Services.AddDbContext<SentinelDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connectionString)) options.UseInMemoryDatabase("sentinellan-development");
    else options.UseNpgsql(connectionString);
});
var signingKey = builder.Configuration["SENTINELLAN_SIGNING_KEY"] ?? "development-signing-key-change-before-deployment";
var accessTokenSigningKey = builder.Configuration["SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY"] ?? "development-access-token-signing-key-change-before-deployment";
if (!builder.Environment.IsDevelopment() && accessTokenSigningKey.StartsWith("development-", StringComparison.Ordinal))
    throw new InvalidOperationException("SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY must be set outside Development.");
if (accessTokenSigningKey.Length < 32) throw new InvalidOperationException("The access-token signing key must contain at least 32 characters.");
builder.Services.AddSingleton<ICommandSigner>(new HmacCommandSigner(signingKey));
builder.Services.AddSingleton<IAccessTokenService>(new AccessTokenService(accessTokenSigningKey));
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IRefreshTokenProtector, RefreshTokenProtector>();
builder.Services.AddSingleton(new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAuthenticationStore, AuthenticationStore>();
builder.Services.AddScoped<SentinelLAN.Application.AuthenticationService>();
builder.Services.AddSingleton<AuthCookieManager>();
builder.Services
    .AddAuthentication(SentinelAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, AccessTokenAuthenticationHandler>(SentinelAuthenticationDefaults.Scheme, _ => { })
    .AddScheme<AuthenticationSchemeOptions, AgentAuthenticationHandler>(AgentAuthenticationDefaults.Scheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy(AuthorizationPolicies.Technician, policy => policy.RequireRole(Roles.Admin, Roles.Technician));
    options.AddPolicy(AuthorizationPolicies.Employee, policy => policy.RequireRole(Roles.Employee));
    options.AddPolicy(AuthorizationPolicies.Agent, policy =>
    {
        policy.AddAuthenticationSchemes(AgentAuthenticationDefaults.Scheme);
        policy.RequireAuthenticatedUser();
        policy.RequireRole(Roles.Agent);
    });
    options.AddPolicy(AuthorizationPolicies.ViewDevices, policy => policy.RequireRole(Roles.Admin, Roles.Technician));
    options.AddPolicy(AuthorizationPolicies.ViewAssignedDevice, policy => policy.RequireRole(Roles.Employee));
    options.AddPolicy(AuthorizationPolicies.ManageCommands, policy => policy.RequireRole(Roles.Admin, Roles.Technician));
    options.AddPolicy(AuthorizationPolicies.ViewAudit, policy => policy.RequireRole(Roles.Admin));
});

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Correlation-ID"] = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    await next();
});
app.UseCors();
app.Use(async (context, next) =>
{
    var unsafeMethod = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
    var cookieAuthenticated = context.Request.Cookies.ContainsKey(AuthCookieManager.AccessCookieName) || context.Request.Cookies.ContainsKey(AuthCookieManager.RefreshCookieName);
    var isLogin = context.Request.Path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase);
    var isHub = context.Request.Path.StartsWithSegments("/hubs");
    if (unsafeMethod && cookieAuthenticated && !isLogin && !isHub && context.Request.Headers[AuthCookieManager.CsrfHeaderName] != "1")
    {
        await Results.Problem("The anti-CSRF header is required.", statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

using (var scope = app.Services.CreateScope())
    await DemoSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<SentinelDbContext>(), scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), key => builder.Configuration[key]);

app.MapGet("/", () => Results.Ok(new { product = "SentinelLAN", version = "0.1.0", openApi = "/openapi/v1.json" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (SentinelDbContext db, CancellationToken ct) => await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.Problem("Database unavailable", statusCode: 503));

var v1 = app.MapGroup("/api/v1");
v1.MapPost("/auth/login", async (LoginRequest request, HttpContext http, SentinelLAN.Application.AuthenticationService authentication, AuthCookieManager cookies, CancellationToken ct) =>
{
    var session = await authentication.LoginAsync(request, ct);
    if (session is null) return Results.Unauthorized();
    cookies.Write(http, session);
    return Results.Ok(new AuthSessionResponse((int)(session.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds, session.Role, session.DisplayName));
}).RequireRateLimiting("sensitive");

v1.MapPost("/auth/refresh", async (HttpContext http, SentinelLAN.Application.AuthenticationService authentication, AuthCookieManager cookies, CancellationToken ct) =>
{
    var result = await authentication.RefreshAsync(http.Request.Cookies[AuthCookieManager.RefreshCookieName], ct);
    if (result.Status != RefreshStatus.Succeeded || result.Session is null)
    {
        cookies.Delete(http);
        return Results.Unauthorized();
    }
    cookies.Write(http, result.Session);
    return Results.Ok(new AuthSessionResponse((int)(result.Session.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds, result.Session.Role, result.Session.DisplayName));
}).RequireRateLimiting("sensitive");

v1.MapPost("/auth/logout", async (HttpContext http, SentinelLAN.Application.AuthenticationService authentication, AuthCookieManager cookies, CancellationToken ct) =>
{
    await authentication.LogoutAsync(http.Request.Cookies[AuthCookieManager.RefreshCookieName], ct);
    cookies.Delete(http);
    return Results.NoContent();
}).RequireRateLimiting("sensitive");

v1.MapGet("/auth/session", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var user = await db.Users
        .AsNoTracking()
        .SingleAsync(item => item.Id == actor.UserId && item.OrganizationId == actor.OrganizationId, ct);
    return Results.Ok(new CurrentSessionResponse(user.Role, user.DisplayName));
}).RequireAuthorization();

v1.MapGet("/devices", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var now = DateTimeOffset.UtcNow;
    var devices = await DeviceScope.ForActor(db.Devices, actor).ToListAsync(ct);
    return Results.Ok(devices.Select(x => new DeviceDto(x.Id, x.Name, x.OsVersion, x.AgentVersion, x.LastSeenAt, x.IsOnline(now))));
}).RequireAuthorization(AuthorizationPolicies.ViewDevices);
v1.MapGet("/devices/{id:guid}", async (Guid id, HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var device = await DeviceScope.ForActor(db.Devices, actor).SingleOrDefaultAsync(x => x.Id == id, ct);
    return device is null ? Results.NotFound() : Results.Ok(new DeviceDto(device.Id, device.Name, device.OsVersion, device.AgentVersion, device.LastSeenAt, device.IsOnline(DateTimeOffset.UtcNow)));
}).RequireAuthorization();
v1.MapGet("/devices/{id:guid}/telemetry", async (Guid id, HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var canAccess = await DeviceScope.ForActor(db.Devices, actor).AnyAsync(x => x.Id == id, ct);
    if (!canAccess) return Results.NotFound();
    var snapshots = await db.Telemetry
        .Where(x => x.DeviceId == id && x.OrganizationId == actor.OrganizationId)
        .OrderByDescending(x => x.CreatedAt)
        .Take(100)
        .Select(x => new TelemetrySnapshotDto(x.Id, x.DeviceId, x.CpuPercent, x.RamPercent, x.DiskPercent, x.CreatedAt))
        .ToListAsync(ct);
    return Results.Ok(snapshots);
}).RequireAuthorization();
v1.MapGet("/dashboard", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var now = DateTimeOffset.UtcNow;
    var devices = await DeviceScope.ForActor(db.Devices, actor).ToListAsync(ct);
    var dto = devices.Select(x => new DeviceDto(x.Id, x.Name, x.OsVersion, x.AgentVersion, x.LastSeenAt, x.IsOnline(now))).ToList();
    return Results.Ok(new DashboardDto(dto.Count, dto.Count(x => x.IsOnline), dto.Count(x => !x.IsOnline), await db.Alerts.CountAsync(x => x.OrganizationId == actor.OrganizationId && x.IsOpen, ct), dto));
}).RequireAuthorization(AuthorizationPolicies.ViewDevices);

v1.MapGet("/my-device", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var device = await DeviceScope.ForActor(db.Devices.AsNoTracking(), actor).SingleOrDefaultAsync(ct);
    if (device is null) return Results.NotFound();

    var policyName = await (
        from assignment in db.PolicyAssignments.AsNoTracking()
        join policy in db.Policies.AsNoTracking() on assignment.PolicyId equals policy.Id
        where assignment.OrganizationId == actor.OrganizationId &&
              assignment.DeviceId == device.Id &&
              policy.OrganizationId == actor.OrganizationId
        orderby assignment.CreatedAt descending
        select policy.Name).FirstOrDefaultAsync(ct);
    var actions = await db.AuditLogs
        .AsNoTracking()
        .Where(item => item.OrganizationId == actor.OrganizationId && item.DeviceId == device.Id)
        .OrderByDescending(item => item.CreatedAt)
        .Take(20)
        .Select(item => new EmployeeDeviceActionDto(item.Action, item.Reason, item.Outcome, item.CreatedAt))
        .ToListAsync(ct);
    var dto = new DeviceDto(device.Id, device.Name, device.OsVersion, device.AgentVersion, device.LastSeenAt, device.IsOnline(DateTimeOffset.UtcNow));
    return Results.Ok(new EmployeeDeviceDto(dto, policyName, actions));
}).RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice);

v1.MapPost("/agent/enroll", async (EnrollRequest request, SentinelDbContext db, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    if (!DeviceRequestValidation.IsValid(request)) return Results.BadRequest(new ProblemDetails { Title = "Valid token, device name, OS and Agent version are required", Status = 400 });
    var hash = SecretHash.Create(request.Token);
    var token = await db.EnrollmentTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    var now = DateTimeOffset.UtcNow;
    if (token is null)
    {
        return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token", Status = 400 });
    }
    if (!token.TryUse(now))
    {
        var failureReason = token.UsedAt is not null ? "Token already used" : "Token expired";
        db.Add(new AuditLog
        {
            OrganizationId = token.OrganizationId,
            ActorId = token.Id,
            DeviceId = null,
            Action = "AgentEnrollmentFailed",
            Reason = failureReason,
            Outcome = "Failed"
        });
        await db.SaveChangesAsync(ct);
        return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token", Status = 400 });
    }

    var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var device = new Device { OrganizationId = token.OrganizationId, Name = request.DeviceName, OsVersion = request.OsVersion, AgentVersion = request.AgentVersion };
    db.Add(device);
    db.Add(new DeviceCredential { OrganizationId = token.OrganizationId, DeviceId = device.Id, SecretHash = SecretHash.Create(secret) });
    db.Add(new AuditLog
    {
        OrganizationId = token.OrganizationId,
        ActorId = device.Id,
        DeviceId = device.Id,
        Action = "AgentEnrolled",
        Reason = $"Device '{request.DeviceName}' successfully enrolled.",
        Outcome = "Success"
    });
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateConcurrencyException)
    {
        db.ChangeTracker.Clear();
        db.Add(new AuditLog { OrganizationId = token.OrganizationId, ActorId = token.Id, Action = "AgentEnrollmentFailed", Reason = "Token already used", Outcome = "Failed" });
        await db.SaveChangesAsync(ct);
        return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token", Status = 400 });
    }
    await hub.Clients.Group(TenantGroup.Name(token.OrganizationId)).SendAsync("device-status", new { device.Id, online = false, device.LastSeenAt }, ct);
    return Results.Ok(new EnrollResponse(device.Id, secret));
}).RequireRateLimiting("sensitive");

v1.MapPost("/agent/heartbeat", async (HeartbeatRequest request, HttpContext http, SentinelDbContext db, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var agent = http.User.ToAgentContext()!.Value;
    if (!DeviceRequestValidation.IsValid(request)) return Results.BadRequest(new ProblemDetails { Title = "Valid idempotency key, 0-100 telemetry percentages, OS and Agent version are required", Status = 400 });
    if (await db.Heartbeats.AnyAsync(x => x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && x.IdempotencyKey == request.IdempotencyKey, ct)) return Results.Ok(new { duplicate = true });
    var device = await db.Devices.SingleAsync(x => x.Id == agent.DeviceId && x.OrganizationId == agent.OrganizationId, ct);
    device.LastSeenAt = DateTimeOffset.UtcNow;
    device.OsVersion = request.OsVersion;
    device.AgentVersion = request.AgentVersion;
    db.Add(new DeviceHeartbeat { OrganizationId = device.OrganizationId, DeviceId = device.Id, IdempotencyKey = request.IdempotencyKey, RecordedAt = DateTimeOffset.UtcNow });
    db.Add(new TelemetrySnapshot { OrganizationId = device.OrganizationId, DeviceId = device.Id, CpuPercent = request.CpuPercent, RamPercent = request.RamPercent, DiskPercent = request.DiskPercent });
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateException)
    {
        db.ChangeTracker.Clear();
        if (await db.Heartbeats.AnyAsync(x => x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && x.IdempotencyKey == request.IdempotencyKey, ct))
            return Results.Ok(new { duplicate = true });
        throw;
    }
    await hub.Clients.Group(TenantGroup.Name(device.OrganizationId)).SendAsync("device-status", new { device.Id, online = true, device.LastSeenAt }, ct);
    return Results.Accepted();
}).RequireAuthorization(AuthorizationPolicies.Agent).RequireRateLimiting("sensitive");

v1.MapPost("/commands", async (CreateCommandRequest request, HttpContext http, SentinelDbContext db, ICommandSigner signer, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    if (!request.Confirmed || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || request.ValidForSeconds is < 30 or > 900) return Results.BadRequest(new ProblemDetails { Title = "Confirmation, a reason (up to 1000 characters) and a 30-900 second validity are required", Status = 400 });
    var device = await DeviceScope.ForActor(db.Devices, actor).SingleOrDefaultAsync(x => x.Id == request.DeviceId, ct);
    if (device is null) return Results.NotFound();
    if (device.IsRevoked) return Results.Conflict(new ProblemDetails { Title = "Device is revoked", Status = 409 });
    var command = new DeviceCommand { OrganizationId = device.OrganizationId, DeviceId = device.Id, IssuedByUserId = actor.UserId, Type = request.Type, Reason = request.Reason.Trim(), Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), Signature = "pending", IssuedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(request.ValidForSeconds), Status = DeviceCommandStatus.Pending };
    if (!command.CanDeliver(DateTimeOffset.UtcNow)) return Results.BadRequest(new ProblemDetails { Title = "Unsupported command type", Status = 400 });
    command.Signature = signer.Sign(command);
    db.Add(command);
    db.Add(new AuditLog { OrganizationId = device.OrganizationId, ActorId = actor.UserId, DeviceId = device.Id, Action = $"CommandCreated:{command.Type}", Reason = command.Reason, Outcome = "Pending" });
    await db.SaveChangesAsync(ct);
    await hub.Clients.Group(TenantGroup.Name(device.OrganizationId)).SendAsync("command-status", new { command.Id, status = command.Status.ToString() }, ct);
    return Results.Created($"/api/v1/commands/{command.Id}", command);
}).RequireAuthorization(AuthorizationPolicies.ManageCommands);

v1.MapPost("/agent/commands/poll", async (HttpContext http, SentinelDbContext db, ICommandSigner signer, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var agent = http.User.ToAgentContext()!.Value;
    var now = DateTimeOffset.UtcNow;
    var expired = await db.Commands.Where(x => x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && x.Status == DeviceCommandStatus.Pending && x.ExpiresAt <= now).ToListAsync(ct);
    foreach (var item in expired) item.Status = DeviceCommandStatus.Expired;
    var command = await db.Commands.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(x => x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && x.Status == DeviceCommandStatus.Pending && x.ExpiresAt > now, ct);
    if (command is null) { await db.SaveChangesAsync(ct); return Results.NoContent(); }
    if (!signer.Verify(command)) return Results.Problem("Command signature validation failed", statusCode: 409);
    command.Status = DeviceCommandStatus.Delivered;
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateConcurrencyException) { return Results.NoContent(); }
    return Results.Ok(command);
}).RequireAuthorization(AuthorizationPolicies.Agent).RequireRateLimiting("sensitive");

v1.MapPost("/agent/commands/{id:guid}/result", async (Guid id, CommandResultRequest request, HttpContext http, SentinelDbContext db, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var agent = http.User.ToAgentContext()!.Value;
    var command = await db.Commands.SingleOrDefaultAsync(x => x.Id == id && x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId, ct);
    if (command is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000) return Results.BadRequest();
    if (await db.CommandResults.AnyAsync(x => x.CommandId == id && x.OrganizationId == agent.OrganizationId, ct)) return Results.Ok(new { duplicate = true });
    if (command.Status != DeviceCommandStatus.Delivered || command.ExpiresAt <= DateTimeOffset.UtcNow)
        return Results.Conflict(new ProblemDetails { Title = "Only a delivered, unexpired command can receive a result", Status = 409 });
    command.Status = request.Succeeded ? DeviceCommandStatus.Succeeded : DeviceCommandStatus.Failed;
    db.Add(new CommandResult { OrganizationId = command.OrganizationId, DeviceId = command.DeviceId, CommandId = id, Succeeded = request.Succeeded, Message = request.Message });
    db.Add(new AuditLog { OrganizationId = command.OrganizationId, ActorId = agent.DeviceId, DeviceId = command.DeviceId, Action = $"CommandCompleted:{command.Id:N}:{command.Type}", Reason = command.Reason, Outcome = command.Status.ToString() });
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateException)
    {
        db.ChangeTracker.Clear();
        if (await db.CommandResults.AnyAsync(x => x.CommandId == id && x.OrganizationId == agent.OrganizationId, ct))
            return Results.Ok(new { duplicate = true });
        throw;
    }
    await hub.Clients.Group(TenantGroup.Name(command.OrganizationId)).SendAsync("command-status", new { command.Id, status = command.Status.ToString() }, ct);
    return Results.Accepted();
}).RequireAuthorization(AuthorizationPolicies.Agent);

v1.MapGet("/audit-logs", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    return Results.Ok(await db.AuditLogs.Where(x => x.OrganizationId == actor.OrganizationId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct));
}).RequireAuthorization(AuthorizationPolicies.ViewAudit);

foreach (var resource in new[] { "organizations", "users" })
    v1.MapGet($"/{resource}", () => Results.Ok(Array.Empty<object>())).RequireAuthorization(AuthorizationPolicies.Admin);
foreach (var resource in new[] { "policies", "alerts" })
    v1.MapGet($"/{resource}", () => Results.Ok(Array.Empty<object>())).RequireAuthorization(AuthorizationPolicies.Technician);

app.MapHub<UpdatesHub>("/hubs/updates").RequireAuthorization(AuthorizationPolicies.ViewDevices);
app.Run();

public partial class Program;
