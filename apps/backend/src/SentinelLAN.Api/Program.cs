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
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
var allowedOrigins = (builder.Configuration["SENTINELLAN_WEB_ORIGINS"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("sensitive", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
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
    .AddScheme<AuthenticationSchemeOptions, AccessTokenAuthenticationHandler>(SentinelAuthenticationDefaults.Scheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.ViewDevices, policy => policy.RequireRole(Roles.Admin, Roles.Technician));
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
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var unsafeMethod = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
    var cookieAuthenticated = context.Request.Cookies.ContainsKey(AuthCookieManager.AccessCookieName) || context.Request.Cookies.ContainsKey(AuthCookieManager.RefreshCookieName);
    var isLogin = context.Request.Path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase);
    if (unsafeMethod && cookieAuthenticated && !isLogin && context.Request.Headers[AuthCookieManager.CsrfHeaderName] != "1")
    {
        await Results.Problem("The anti-CSRF header is required.", statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
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
    return Results.Ok(await db.Telemetry.Where(x => x.DeviceId == id && x.OrganizationId == actor.OrganizationId).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct));
}).RequireAuthorization();
v1.MapGet("/dashboard", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var now = DateTimeOffset.UtcNow;
    var devices = await DeviceScope.ForActor(db.Devices, actor).ToListAsync(ct);
    var dto = devices.Select(x => new DeviceDto(x.Id, x.Name, x.OsVersion, x.AgentVersion, x.LastSeenAt, x.IsOnline(now))).ToList();
    return Results.Ok(new DashboardDto(dto.Count, dto.Count(x => x.IsOnline), dto.Count(x => !x.IsOnline), await db.Alerts.CountAsync(x => x.OrganizationId == actor.OrganizationId && x.IsOpen, ct), dto));
}).RequireAuthorization(AuthorizationPolicies.ViewDevices);

v1.MapPost("/agent/enroll", async (EnrollRequest request, SentinelDbContext db, CancellationToken ct) =>
{
    var hash = SecretHash.Create(request.Token);
    var token = await db.EnrollmentTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    if (token is null || !token.TryUse(DateTimeOffset.UtcNow)) return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token", Status = 400 });
    var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var device = new Device { OrganizationId = token.OrganizationId, Name = request.DeviceName, OsVersion = request.OsVersion, AgentVersion = request.AgentVersion };
    db.Add(device);
    db.Add(new DeviceCredential { OrganizationId = token.OrganizationId, DeviceId = device.Id, SecretHash = SecretHash.Create(secret) });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new EnrollResponse(device.Id, secret));
}).RequireRateLimiting("sensitive");

v1.MapPost("/agent/heartbeat", async (HeartbeatRequest request, SentinelDbContext db, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    var credential = await db.DeviceCredentials.SingleOrDefaultAsync(x => x.DeviceId == request.DeviceId && x.RevokedAt == null, ct);
    if (credential is null || !SecretHash.Matches(request.DeviceSecret, credential.SecretHash)) return Results.Unauthorized();
    if (await db.Heartbeats.AnyAsync(x => x.DeviceId == request.DeviceId && x.IdempotencyKey == request.IdempotencyKey, ct)) return Results.Ok(new { duplicate = true });
    var device = await db.Devices.SingleAsync(x => x.Id == request.DeviceId, ct);
    device.LastSeenAt = DateTimeOffset.UtcNow;
    device.OsVersion = request.OsVersion;
    device.AgentVersion = request.AgentVersion;
    db.Add(new DeviceHeartbeat { OrganizationId = device.OrganizationId, DeviceId = device.Id, IdempotencyKey = request.IdempotencyKey, RecordedAt = DateTimeOffset.UtcNow });
    db.Add(new TelemetrySnapshot { OrganizationId = device.OrganizationId, DeviceId = device.Id, CpuPercent = request.CpuPercent, RamPercent = request.RamPercent, DiskPercent = request.DiskPercent });
    await db.SaveChangesAsync(ct);
    await hub.Clients.Group(TenantGroup.Name(device.OrganizationId)).SendAsync("device-status", new { device.Id, online = true, device.LastSeenAt }, ct);
    return Results.Accepted();
}).RequireRateLimiting("sensitive");

v1.MapPost("/commands", async (CreateCommandRequest request, HttpContext http, SentinelDbContext db, ICommandSigner signer, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    if (string.IsNullOrWhiteSpace(request.Reason) || request.ValidForSeconds is < 30 or > 900) return Results.BadRequest(new ProblemDetails { Title = "Reason and a 30-900 second validity are required", Status = 400 });
    var device = await DeviceScope.ForActor(db.Devices, actor).SingleOrDefaultAsync(x => x.Id == request.DeviceId, ct);
    if (device is null) return Results.NotFound();
    var command = new DeviceCommand { OrganizationId = device.OrganizationId, DeviceId = device.Id, IssuedByUserId = actor.UserId, Type = request.Type, Reason = request.Reason.Trim(), Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), Signature = "pending", IssuedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(request.ValidForSeconds), Status = DeviceCommandStatus.Pending };
    if (!command.CanDeliver(DateTimeOffset.UtcNow)) return Results.BadRequest(new ProblemDetails { Title = "Unsupported command type", Status = 400 });
    command.Signature = signer.Sign(command);
    db.Add(command);
    db.Add(new AuditLog { OrganizationId = device.OrganizationId, ActorId = actor.UserId, DeviceId = device.Id, Action = $"CommandCreated:{command.Type}", Reason = command.Reason, Outcome = "Pending" });
    await db.SaveChangesAsync(ct);
    await hub.Clients.Group(TenantGroup.Name(device.OrganizationId)).SendAsync("command-status", new { command.Id, status = command.Status.ToString() }, ct);
    return Results.Created($"/api/v1/commands/{command.Id}", command);
}).RequireAuthorization(AuthorizationPolicies.ManageCommands);

v1.MapPost("/agent/commands/poll", async (Guid deviceId, string deviceSecret, SentinelDbContext db, ICommandSigner signer, CancellationToken ct) =>
{
    var credential = await db.DeviceCredentials.SingleOrDefaultAsync(x => x.DeviceId == deviceId && x.RevokedAt == null, ct);
    if (credential is null || !SecretHash.Matches(deviceSecret, credential.SecretHash)) return Results.Unauthorized();
    var now = DateTimeOffset.UtcNow;
    var expired = await db.Commands.Where(x => x.DeviceId == deviceId && x.Status == DeviceCommandStatus.Pending && x.ExpiresAt <= now).ToListAsync(ct);
    foreach (var item in expired) item.Status = DeviceCommandStatus.Expired;
    var command = await db.Commands.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.Status == DeviceCommandStatus.Pending && x.ExpiresAt > now, ct);
    if (command is null) { await db.SaveChangesAsync(ct); return Results.NoContent(); }
    if (!signer.Verify(command)) return Results.Problem("Command signature validation failed", statusCode: 409);
    command.Status = DeviceCommandStatus.Delivered;
    await db.SaveChangesAsync(ct);
    return Results.Ok(command);
}).RequireRateLimiting("sensitive");

v1.MapPost("/agent/commands/{id:guid}/result", async (Guid id, CommandResultRequest request, SentinelDbContext db, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    var credential = await db.DeviceCredentials.SingleOrDefaultAsync(x => x.DeviceId == request.DeviceId && x.RevokedAt == null, ct);
    if (credential is null || !SecretHash.Matches(request.DeviceSecret, credential.SecretHash)) return Results.Unauthorized();
    var command = await db.Commands.SingleOrDefaultAsync(x => x.Id == id && x.DeviceId == request.DeviceId, ct);
    if (command is null) return Results.NotFound();
    if (await db.CommandResults.AnyAsync(x => x.CommandId == id, ct)) return Results.Ok(new { duplicate = true });
    command.Status = request.Succeeded ? DeviceCommandStatus.Succeeded : DeviceCommandStatus.Failed;
    db.Add(new CommandResult { OrganizationId = command.OrganizationId, DeviceId = command.DeviceId, CommandId = id, Succeeded = request.Succeeded, Message = request.Message });
    var audit = await db.AuditLogs.SingleAsync(x => x.OrganizationId == command.OrganizationId && x.DeviceId == command.DeviceId && x.Action == $"CommandCreated:{command.Type}" && x.Outcome == "Pending", ct);
    audit.Outcome = command.Status.ToString();
    await db.SaveChangesAsync(ct);
    await hub.Clients.Group(TenantGroup.Name(command.OrganizationId)).SendAsync("command-status", new { command.Id, status = command.Status.ToString() }, ct);
    return Results.Accepted();
});

v1.MapGet("/audit-logs", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    return Results.Ok(await db.AuditLogs.Where(x => x.OrganizationId == actor.OrganizationId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct));
}).RequireAuthorization(AuthorizationPolicies.ViewAudit);

foreach (var resource in new[] { "organizations", "users", "policies", "alerts" })
    v1.MapGet($"/{resource}", () => Results.Ok(Array.Empty<object>())).RequireAuthorization();

app.MapHub<UpdatesHub>("/hubs/updates").RequireAuthorization();
app.Run();

public partial class Program;
