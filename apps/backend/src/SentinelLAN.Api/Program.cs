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
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings__SentinelLAN is required outside Development.");
builder.Services.AddDbContext<SentinelDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connectionString)) options.UseInMemoryDatabase("sentinellan-development");
    else options.UseNpgsql(connectionString);
});
var signingKey = builder.Configuration["SENTINELLAN_SIGNING_KEY"] ?? "development-signing-key-change-before-deployment";
var accessTokenSigningKey = builder.Configuration["SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY"] ?? "development-access-token-signing-key-change-before-deployment";
var serverVaultKey = builder.Configuration["SENTINELLAN_SERVER_VAULT_KEY"] ?? "development-server-vault-key-32-bytes-long!";
if (!builder.Environment.IsDevelopment())
{
    if (IsUnsafeProductionSecret(signingKey))
        throw new InvalidOperationException("SENTINELLAN_SIGNING_KEY must be a unique secret of at least 32 characters outside Development.");
    if (IsUnsafeProductionSecret(accessTokenSigningKey))
        throw new InvalidOperationException("SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY must be set outside Development.");
    if (IsUnsafeProductionSecret(serverVaultKey))
        throw new InvalidOperationException("SENTINELLAN_SERVER_VAULT_KEY must be a unique secret of at least 32 characters outside Development.");
}
if (accessTokenSigningKey.Length < 32) throw new InvalidOperationException("The access-token signing key must contain at least 32 characters.");
builder.Services.AddSingleton<ICommandSigner>(new HmacCommandSigner(signingKey));
builder.Services.AddSingleton<IAccessTokenService>(new AccessTokenService(accessTokenSigningKey));
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IRefreshTokenProtector, RefreshTokenProtector>();
builder.Services.AddSingleton(new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAuthenticationStore, AuthenticationStore>();
builder.Services.AddScoped<SentinelLAN.Application.AuthenticationService>();
builder.Services.AddScoped<IManagementStore, ManagementStore>();
builder.Services.AddSingleton<IEnrollmentSecretGenerator, EnrollmentSecretGenerator>();
builder.Services.AddSingleton<ISecretHasher, SecretHasher>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<EnrollmentTokenService>();
builder.Services.AddScoped<DeviceManagementService>();
builder.Services.AddScoped<IPolicyStore, PolicyStore>();
builder.Services.AddScoped<PolicyService>();
builder.Services.AddScoped<IAlertStore, AlertStore>();
builder.Services.AddScoped<IAlertDeviceLookup, AlertDeviceLookup>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddSingleton<IVpsVaultService>(new VpsVaultService(serverVaultKey));
builder.Services.AddSingleton<IVpsSshService, SshNetVpsSshService>();
builder.Services.AddScoped<IVpsNodeStore, VpsNodeStore>();
builder.Services.AddScoped<VpsNodeService>();
builder.Services.AddScoped<IAssetStore, AssetStore>();
builder.Services.AddScoped<IAssetManagementService, AssetManagementService>();
builder.Services.AddSingleton<IActivationTokenGenerator, ActivationTokenGenerator>();
builder.Services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
builder.Services.AddScoped<IQrManagementService, QrManagementService>();
builder.Services.AddScoped<IMyDeviceStore, MyDeviceStore>();
builder.Services.AddScoped<IMyDeviceService, MyDeviceService>();
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
    options.AddPolicy(AuthorizationPolicies.ViewPolicies, policy => policy.RequireRole(Roles.Admin, Roles.Technician));
    options.AddPolicy(AuthorizationPolicies.ManagePolicies, policy => policy.RequireRole(Roles.Admin, Roles.Technician));
    options.AddPolicy(AuthorizationPolicies.ViewAlerts, policy => policy.RequireRole(Roles.Admin, Roles.Technician));
    options.AddPolicy(AuthorizationPolicies.ManageAlerts, policy => policy.RequireRole(Roles.Admin, Roles.Technician));
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
{
    var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    if (app.Environment.IsDevelopment())
        await DemoSeeder.SeedAsync(db, passwordHasher, key => builder.Configuration[key]);
    else
    {
        await db.Database.MigrateAsync();
        await BootstrapOrganizationInitializer.EnsureCreatedAsync(db, passwordHasher,
            builder.Configuration["SENTINELLAN_BOOTSTRAP_ORG_CODE"],
            builder.Configuration["SENTINELLAN_BOOTSTRAP_ORG_NAME"],
            builder.Configuration["SENTINELLAN_BOOTSTRAP_ADMIN_EMAIL"],
            builder.Configuration["SENTINELLAN_BOOTSTRAP_ADMIN_PASSWORD"]);
    }
}

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

var mobile = v1.MapGroup("/mobile");
var mobileAuth = mobile.MapGroup("/auth");

mobileAuth.MapPost("/login", async (MobileLoginRequest request, HttpContext http, SentinelLAN.Application.AuthenticationService authentication, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var result = await authentication.LoginMobileAsync(request, ct);
    if (result is null) return Results.Unauthorized();
    return Results.Ok(new MobileAuthSessionResponse(
        result.AccessToken,
        (int)(result.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
        result.RefreshToken,
        result.RefreshTokenExpiresAt,
        "Bearer",
        new MobileUserInfo(result.UserId, result.Email, result.DisplayName, result.Role, result.OrganizationId, result.OrganizationCode)
    ));
}).AllowAnonymous().RequireRateLimiting("sensitive");

mobileAuth.MapPost("/refresh", async (MobileRefreshRequest request, HttpContext http, SentinelLAN.Application.AuthenticationService authentication, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    if (string.IsNullOrWhiteSpace(request.RefreshToken)) return Results.Unauthorized();
    var result = await authentication.RefreshMobileAsync(request.RefreshToken, ct);
    if (result.Status != RefreshStatus.Succeeded || result.Result is null)
    {
        return Results.Unauthorized();
    }
    return Results.Ok(new MobileRefreshResponse(
        result.Result.AccessToken,
        (int)(result.Result.AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
        result.Result.RefreshToken,
        result.Result.RefreshTokenExpiresAt,
        "Bearer"
    ));
}).AllowAnonymous().RequireRateLimiting("sensitive");

mobileAuth.MapPost("/logout", async (MobileLogoutRequest request, HttpContext http, SentinelLAN.Application.AuthenticationService authentication, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    await authentication.LogoutMobileAsync(request.RefreshToken, ct);
    return Results.NoContent();
}).AllowAnonymous().RequireRateLimiting("sensitive");

mobileAuth.MapPost("/logout-all", async (HttpContext http, SentinelLAN.Application.AuthenticationService authentication, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var actor = http.User.ToActorContext()!.Value;
    await authentication.LogoutAllUserSessionsAsync(actor.OrganizationId, actor.UserId, ct);
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("sensitive");

mobile.MapGet("/bootstrap", (HttpContext http) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    return Results.Ok(new MobileBootstrapResponse(
        MinimumAppVersion: "1.0.0",
        LatestAppVersion: "1.0.0",
        PrivacyManifestVersion: "2026.1",
        MaintenanceMode: false,
        SupportEmail: "it-support@sentinellan.local",
        SupportedAuthSchemes: ["Bearer"]
    ));
}).AllowAnonymous();

v1.MapPost("/auth/activation/validate", async (ValidateActivationTokenRequest req, UserManagementService users, HttpContext http, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var result = await users.ValidateActivationTokenAsync(req.Token, ct);
    return Results.Ok(result);
}).AllowAnonymous().RequireRateLimiting("sensitive");

v1.MapPost("/auth/activate", async (ActivateAccountRequest req, UserManagementService users, IPasswordHasher passwordHasher, SentinelDbContext db, HttpContext http, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var (status, message) = await users.ActivateAccountAsync(req, passwordHasher, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Ok(new { message }),
        ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Activation conflict", Detail = message, Status = 409 }),
        _ => Results.BadRequest(new ProblemDetails { Title = "Activation failed", Detail = message, Status = 400 })
    };
}).AllowAnonymous().RequireRateLimiting("sensitive");

v1.MapPost("/users", async (CreateUserRequest request, HttpContext http, SentinelDbContext db, UserManagementService users, IPasswordHasher passwordHasher, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var actor = http.User.ToActorContext()!.Value;
    var result = await users.CreateAsync(actor, request, passwordHasher, ct);
    return result.Status switch
    {
        ManagementResultStatus.Succeeded => Results.Created($"/api/v1/users/{result.User!.Id}", new CreateUserResponse(result.User, result.ActivationToken, result.ActivationUrl, result.ExpiresAt)),
        ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "A user with that email already exists", Status = 409 }),
        _ => Results.BadRequest(new ProblemDetails { Title = "Invalid user details", Status = 400 })
    };
})
    .WithName("CreateUser")
    .Produces<CreateUserResponse>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .RequireAuthorization(AuthorizationPolicies.Admin);

v1.MapPut("/users/{id:guid}/status", async (Guid id, SetUserStatusRequest request, HttpContext http, SentinelDbContext db, UserManagementService users, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var actor = http.User.ToActorContext()!.Value;
    var (status, user) = await users.SetStatusAsync(actor, id, request, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Ok(user),
        ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        ManagementResultStatus.NotFound => Results.NotFound(),
        ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Account state cannot be changed", Status = 409 }),
        _ => Results.BadRequest(new ProblemDetails { Title = "Status, reason and confirmation are required", Status = 400 })
    };
}).WithName("SetUserStatus")
  .Produces<UserSummaryDto>(StatusCodes.Status200OK)
  .ProducesProblem(StatusCodes.Status400BadRequest)
  .ProducesProblem(StatusCodes.Status403Forbidden)
  .ProducesProblem(StatusCodes.Status404NotFound)
  .ProducesProblem(StatusCodes.Status409Conflict)
  .RequireAuthorization(AuthorizationPolicies.Admin)
  .RequireRateLimiting("sensitive");

v1.MapPost("/users/{id:guid}/activation-token", async (Guid id, ReissueActivationTokenRequest req, HttpContext http, SentinelDbContext db, UserManagementService users, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var actor = http.User.ToActorContext()!.Value;
    var (status, response, message) = await users.ReissueActivationTokenAsync(actor, id, req, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Ok(response),
        ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "User not found", Detail = message, Status = 404 }),
        ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Conflict", Detail = message, Status = 409 }),
        _ => Results.BadRequest(new ProblemDetails { Title = "Invalid request", Detail = message, Status = 400 })
    };
}).RequireAuthorization(AuthorizationPolicies.Admin);

v1.MapDelete("/users/{id:guid}/activation-token", async (Guid id, [FromBody] RevokeActivationTokenRequest? req, HttpContext http, SentinelDbContext db, UserManagementService users, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var actor = http.User.ToActorContext()!.Value;
    var request = req ?? new RevokeActivationTokenRequest("Revoked by administrator", true);
    var (status, message) = await users.RevokeActivationTokenAsync(actor, id, request, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.NoContent(),
        ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "User not found", Detail = message, Status = 404 }),
        ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        _ => Results.BadRequest(new ProblemDetails { Title = "Invalid request", Detail = message, Status = 400 })
    };
}).RequireAuthorization(AuthorizationPolicies.Admin);

v1.MapPost("/enrollment-tokens", async (EnrollmentTokenRequest request, HttpContext http, SentinelDbContext db, EnrollmentTokenService tokens, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var actor = http.User.ToActorContext()!.Value;
    var result = await tokens.CreateAsync(actor, request, ct);
    if (result.Status == ManagementResultStatus.Forbidden) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (result.Status != ManagementResultStatus.Succeeded) return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token details", Status = 400 });
    http.Response.Headers.CacheControl = "no-store";
    return Results.Created("/api/v1/enrollment-tokens", result.Token);
})
    .WithName("CreateEnrollmentToken")
    .Produces<EnrollmentTokenResponse>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .RequireAuthorization(AuthorizationPolicies.Admin)
    .RequireRateLimiting("sensitive");

v1.MapPut("/devices/{id:guid}/assignment", async (Guid id, DeviceAssignmentRequest request, HttpContext http, SentinelDbContext db, DeviceManagementService devices, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var actor = http.User.ToActorContext()!.Value;
    var result = await devices.AssignAsync(actor, id, request, ct);
    return result.Status switch
    {
        ManagementResultStatus.Succeeded => await PublishDeviceUpdateAsync(result.Device!, hub, http, ct),
        ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        ManagementResultStatus.NotFound => Results.NotFound(),
        ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Device assignment conflicts with an existing active assignment", Status = 409 }),
        _ => Results.BadRequest(new ProblemDetails { Title = "Invalid assignment details", Status = 400 })
    };
})
    .WithName("AssignDevice")
    .Produces<DeviceDto>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .RequireAuthorization(AuthorizationPolicies.Admin);

v1.MapPost("/devices/{id:guid}/revoke", async (Guid id, RevokeDeviceRequest request, HttpContext http, SentinelDbContext db, DeviceManagementService devices, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var actor = http.User.ToActorContext()!.Value;
    var result = await devices.RevokeAsync(actor, id, request, ct);
    return result.Status switch
    {
        ManagementResultStatus.Succeeded => await PublishDeviceUpdateAsync(result.Device!, hub, http, ct),
        ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        ManagementResultStatus.NotFound => Results.NotFound(),
        ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Device could not be revoked", Status = 409 }),
        _ => Results.BadRequest(new ProblemDetails { Title = "Confirmation and a valid reason are required", Status = 400 })
    };
})
    .WithName("RevokeDevice")
    .Produces<DeviceDto>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .RequireAuthorization(AuthorizationPolicies.Admin);

v1.MapGet("/devices", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var now = DateTimeOffset.UtcNow;
    var devices = await DeviceScope.ForActor(db.Devices, actor).ToListAsync(ct);
    return Results.Ok(devices.Select(x => new DeviceDto(x.Id, x.Name, x.OsVersion, x.AgentVersion, x.LastSeenAt, x.IsOnline(now), x.AssignedUserId, x.IsRevoked)));
}).RequireAuthorization(AuthorizationPolicies.ViewDevices);
v1.MapGet("/devices/{id:guid}", async (Guid id, HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var device = await DeviceScope.ForActor(db.Devices, actor).SingleOrDefaultAsync(x => x.Id == id, ct);
    return device is null ? Results.NotFound() : Results.Ok(new DeviceDto(device.Id, device.Name, device.OsVersion, device.AgentVersion, device.LastSeenAt, device.IsOnline(DateTimeOffset.UtcNow), device.AssignedUserId, device.IsRevoked));
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
    var dto = devices.Select(x => new DeviceDto(x.Id, x.Name, x.OsVersion, x.AgentVersion, x.LastSeenAt, x.IsOnline(now), x.AssignedUserId, x.IsRevoked)).ToList();
    return Results.Ok(new DashboardDto(dto.Count, dto.Count(x => x.IsOnline), dto.Count(x => !x.IsOnline), await db.Alerts.CountAsync(x => x.OrganizationId == actor.OrganizationId && x.IsOpen, ct), dto));
}).RequireAuthorization(AuthorizationPolicies.ViewDevices);

v1.MapGet("/my-device", async (HttpContext http, IMyDeviceService myDeviceService, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var actor = http.User.ToActorContext()!.Value;
    var device = await myDeviceService.GetMyDeviceAsync(actor, ct);
    return device is null ? Results.NotFound() : Results.Ok(device);
}).RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).RequireRateLimiting("sensitive");

v1.MapGet("/my-device/telemetry", async ([FromQuery] int? limit, HttpContext http, IMyDeviceService myDeviceService, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var actor = http.User.ToActorContext()!.Value;
    var history = await myDeviceService.GetMyDeviceTelemetryAsync(actor, limit ?? 10, ct);
    return Results.Ok(history);
}).RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).RequireRateLimiting("sensitive");

v1.MapGet("/my-device/incidents", async (HttpContext http, IMyDeviceService myDeviceService, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var actor = http.User.ToActorContext()!.Value;
    var incidents = await myDeviceService.GetMyDeviceIncidentsAsync(actor, ct);
    return Results.Ok(incidents);
}).RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).RequireRateLimiting("sensitive");

v1.MapPost("/my-device/incidents", async (ReportMyDeviceIncidentRequest req, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext http, IMyDeviceService myDeviceService, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var actor = http.User.ToActorContext()!.Value;
    var (status, incident, message) = await myDeviceService.ReportIncidentAsync(actor, req with { IdempotencyKey = idempotencyKey }, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Created($"/api/v1/my-device/incidents/{incident!.Id}", incident),
        ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "Device not found", Detail = message, Status = 404 }),
        ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Idempotency conflict", Detail = message, Status = 409 }),
        ManagementResultStatus.Invalid => Results.BadRequest(new ProblemDetails { Title = "Invalid incident request", Detail = message, Status = 400 }),
        _ => Results.BadRequest(new ProblemDetails { Title = "Failed to report incident", Detail = message, Status = 400 })
    };
}).RequireAuthorization(AuthorizationPolicies.ViewAssignedDevice).RequireRateLimiting("sensitive");

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
    var command = new DeviceCommand
    {
        OrganizationId = device.OrganizationId,
        DeviceId = device.Id,
        IssuedByUserId = actor.UserId,
        Type = request.Type,
        Reason = request.Reason.Trim(),
        Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
        Signature = "pending",
        Parameter = request.Parameter?.Trim(),
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(request.ValidForSeconds),
        Status = DeviceCommandStatus.Pending
    };
    if (!command.CanDeliver(DateTimeOffset.UtcNow)) return Results.BadRequest(new ProblemDetails { Title = "Unsupported command type", Status = 400 });
    command.Signature = signer.Sign(command);
    db.Add(command);
    db.Add(new AuditLog { OrganizationId = device.OrganizationId, ActorId = actor.UserId, DeviceId = device.Id, Action = $"CommandCreated:{command.Type}", Reason = command.Reason, Outcome = "Pending" });
    await db.SaveChangesAsync(ct);
    await hub.Clients.Group(TenantGroup.Name(device.OrganizationId)).SendAsync("command-status", new { command.Id, status = command.Status.ToString() }, ct);
    return Results.Created($"/api/v1/commands/{command.Id}", command);
}).RequireAuthorization(AuthorizationPolicies.ManageCommands);

v1.MapGet("/commands", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var commands = await (
        from c in db.Commands.AsNoTracking()
        join d in db.Devices.AsNoTracking() on c.DeviceId equals d.Id
        where c.OrganizationId == actor.OrganizationId
        orderby c.CreatedAt descending
        select new
        {
            Command = c,
            DeviceName = d.Name,
            Result = db.CommandResults.AsNoTracking().FirstOrDefault(r => r.CommandId == c.Id)
        })
        .Take(100)
        .ToListAsync(ct);

    return Results.Ok(commands.Select(x => new CommandDto(
        x.Command.Id,
        x.Command.DeviceId,
        x.DeviceName,
        x.Command.Type,
        x.Command.Reason,
        x.Command.Parameter,
        x.Command.Status.ToString(),
        x.Command.IssuedAt,
        x.Command.ExpiresAt,
        x.Result?.Succeeded,
        x.Result?.Message
    )));
}).RequireAuthorization(AuthorizationPolicies.ManageCommands);

v1.MapGet("/commands/{id:guid}", async (Guid id, HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var item = await (
        from c in db.Commands.AsNoTracking()
        join d in db.Devices.AsNoTracking() on c.DeviceId equals d.Id
        where c.OrganizationId == actor.OrganizationId && c.Id == id
        select new
        {
            Command = c,
            DeviceName = d.Name,
            Result = db.CommandResults.AsNoTracking().FirstOrDefault(r => r.CommandId == c.Id)
        }).SingleOrDefaultAsync(ct);

    if (item is null) return Results.NotFound();

    return Results.Ok(new CommandDto(
        item.Command.Id,
        item.Command.DeviceId,
        item.DeviceName,
        item.Command.Type,
        item.Command.Reason,
        item.Command.Parameter,
        item.Command.Status.ToString(),
        item.Command.IssuedAt,
        item.Command.ExpiresAt,
        item.Result?.Succeeded,
        item.Result?.Message
    ));
}).RequireAuthorization(AuthorizationPolicies.ManageCommands);

v1.MapPost("/agent/commands/poll", async (HttpContext http, SentinelDbContext db, ICommandSigner signer, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var agent = http.User.ToAgentContext()!.Value;
    var now = DateTimeOffset.UtcNow;
    var expired = await db.Commands.Where(x => x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && (x.Status == DeviceCommandStatus.Pending || x.Status == DeviceCommandStatus.Delivered) && x.ExpiresAt <= now).ToListAsync(ct);
    foreach (var item in expired)
    {
        item.Status = DeviceCommandStatus.Expired;
        item.DeliveryLeaseExpiresAt = null;
    }
    var command = await db.Commands.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(x =>
        x.DeviceId == agent.DeviceId && x.OrganizationId == agent.OrganizationId && x.ExpiresAt > now &&
        (x.Status == DeviceCommandStatus.Pending ||
         (x.Status == DeviceCommandStatus.Delivered && x.DeliveryLeaseExpiresAt <= now)), ct);
    if (command is null)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.NoContent(); }
        return Results.NoContent();
    }
    if (!signer.Verify(command)) return Results.Problem("Command signature validation failed", statusCode: 409);
    if (!command.TryLeaseForDelivery(now, TimeSpan.FromSeconds(30))) return Results.NoContent();
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
    command.DeliveryLeaseExpiresAt = null;
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

// POLICIES
v1.MapGet("/policies", async (HttpContext http, PolicyService policyService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    return Results.Ok(await policyService.GetPoliciesAsync(actor, ct));
}).RequireAuthorization(AuthorizationPolicies.ViewPolicies);

v1.MapPost("/policies", async (CreatePolicyRequest request, HttpContext http, PolicyService policyService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var policy = await policyService.CreatePolicyAsync(actor, request, ct);
    return policy is not null ? Results.Created($"/api/v1/policies/{policy.Id}", policy) : Results.BadRequest(new ProblemDetails { Title = "Invalid policy details or name already in use", Status = 400 });
}).RequireAuthorization(AuthorizationPolicies.ManagePolicies);

v1.MapPut("/policies/{id:guid}", async (Guid id, UpdatePolicyRequest request, HttpContext http, PolicyService policyService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var policy = await policyService.UpdatePolicyAsync(actor, id, request, ct);
    return policy is not null ? Results.Ok(policy) : Results.BadRequest(new ProblemDetails { Title = "Invalid policy details or policy not found", Status = 400 });
}).RequireAuthorization(AuthorizationPolicies.ManagePolicies);

v1.MapPost("/policies/{id:guid}/assign", async (Guid id, AssignPolicyRequest request, HttpContext http, PolicyService policyService, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    if (id != request.PolicyId) return Results.BadRequest(new ProblemDetails { Title = "Route ID must match payload PolicyId", Status = 400 });
    var succeeded = await policyService.AssignPolicyAsync(actor, request, ct);
    if (!succeeded) return Results.BadRequest(new ProblemDetails { Title = "Unable to assign policy to specified device", Status = 400 });
    await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("policy-assigned", new { policyId = id, deviceId = request.DeviceId }, ct);
    return Results.Ok(new { assigned = true });
}).RequireAuthorization(AuthorizationPolicies.ManagePolicies);

// ALERTS
v1.MapGet("/alerts", async (HttpContext http, AlertService alertService, [FromQuery] bool? onlyOpen, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    return Results.Ok(await alertService.GetAlertsAsync(actor, onlyOpen, ct));
}).RequireAuthorization(AuthorizationPolicies.ViewAlerts);

v1.MapPost("/alerts", async (CreateAlertRequest request, HttpContext http, AlertService alertService, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var alert = await alertService.TriggerAlertAsync(actor, request.DeviceId, request.Severity, request.Message, ct);
    if (alert is null) return Results.BadRequest();
    await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("alert-triggered", alert, ct);
    return Results.Created($"/api/v1/alerts/{alert.Id}", alert);
}).RequireAuthorization(AuthorizationPolicies.ManageAlerts);

v1.MapPost("/alerts/{id:guid}/acknowledge", async (Guid id, HttpContext http, AlertService alertService, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var succeeded = await alertService.AcknowledgeAlertAsync(actor, id, ct);
    if (!succeeded) return Results.NotFound();
    await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("alert-updated", new { id, action = "acknowledged" }, ct);
    return Results.Ok(new { acknowledged = true });
}).RequireAuthorization(AuthorizationPolicies.ManageAlerts);

v1.MapPost("/alerts/{id:guid}/resolve", async (Guid id, HttpContext http, AlertService alertService, IHubContext<UpdatesHub> hub, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var succeeded = await alertService.ResolveAlertAsync(actor, id, ct);
    if (!succeeded) return Results.NotFound();
    await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("alert-updated", new { id, action = "resolved" }, ct);
    return Results.Ok(new { resolved = true });
}).RequireAuthorization(AuthorizationPolicies.ManageAlerts);

// AUDIT LOGS
v1.MapGet("/audit-logs", async (HttpContext http, SentinelDbContext db, [FromQuery] Guid? deviceId, [FromQuery] string? action, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var query = db.AuditLogs.AsNoTracking().Where(x => x.OrganizationId == actor.OrganizationId);
    if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId.Value);
    if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action.Contains(action.Trim()));

    var logs = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);

    var actorIds = logs.Select(x => x.ActorId).Distinct().ToList();
    var devIds = logs.Where(x => x.DeviceId.HasValue).Select(x => x.DeviceId!.Value).Distinct().ToList();

    var userNames = await db.Users.AsNoTracking().Where(u => u.OrganizationId == actor.OrganizationId && actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    var deviceNames = await db.Devices.AsNoTracking().Where(d => d.OrganizationId == actor.OrganizationId && (devIds.Contains(d.Id) || actorIds.Contains(d.Id))).ToDictionaryAsync(d => d.Id, d => d.Name, ct);

    return Results.Ok(logs.Select(x => new AuditLogDto(
        x.Id,
        x.ActorId,
        userNames.GetValueOrDefault(x.ActorId) ?? deviceNames.GetValueOrDefault(x.ActorId) ?? "System",
        x.DeviceId,
        x.DeviceId.HasValue ? deviceNames.GetValueOrDefault(x.DeviceId.Value) : null,
        x.Action,
        x.Reason,
        x.Outcome,
        x.CreatedAt
    )));
}).RequireAuthorization(AuthorizationPolicies.ViewAudit);

// USERS & ORGANIZATIONS
v1.MapGet("/users", async (HttpContext http, UserManagementService users, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var list = await users.GetUsersAsync(actor, ct);
    return Results.Ok(list);
}).RequireAuthorization(AuthorizationPolicies.Admin);

v1.MapGet("/organizations", async (HttpContext http, SentinelDbContext db, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var org = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == actor.OrganizationId, ct);
    return org is null ? Results.NotFound() : Results.Ok(new OrganizationSummaryDto(org.Id, org.Code, org.Name, org.CreatedAt));
}).RequireAuthorization(AuthorizationPolicies.Admin);

// CLOUD VPS NODES (AGENTLESS SSH)
v1.MapGet("/vps-nodes", async (HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var nodes = await vpsService.GetNodesAsync(actor, ct);
    return Results.Ok(nodes);
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapGet("/vps-nodes/{id:guid}", async (Guid id, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var node = await vpsService.GetNodeByIdAsync(actor, id, ct);
    return node is null ? Results.NotFound() : Results.Ok(node);
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapPost("/vps-nodes", async (CreateVpsNodeRequest req, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var created = await vpsService.CreateNodeAsync(actor, req, ct);
    return created is null
        ? Results.BadRequest(new ProblemDetails { Title = "Invalid VPS node data", Detail = "Name, host, port, username, a valid OpenSSH private key, and a verified SHA256 host key fingerprint are required." })
        : Results.Created($"/api/v1/vps-nodes/{created.Id}", created);
}).RequireAuthorization(AuthorizationPolicies.Admin);

v1.MapDelete("/vps-nodes/{id:guid}", async (Guid id, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var deleted = await vpsService.DeleteNodeAsync(actor, id, ct);
    return deleted ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization(AuthorizationPolicies.Admin);

v1.MapPost("/vps-nodes/{id:guid}/test-connection", async (Guid id, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var result = await vpsService.TestConnectionAsync(actor, id, ct);
    return Results.Ok(result);
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapPost("/vps-nodes/{id:guid}/refresh-metrics", async (Guid id, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var node = await vpsService.RefreshMetricsAsync(actor, id, ct);
    return node is null ? Results.NotFound() : Results.Ok(node);
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapPost("/vps-nodes/{id:guid}/restart-service", async (Guid id, RestartVpsServiceRequest req, HttpContext http, VpsNodeService vpsService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var result = await vpsService.RestartServiceAsync(actor, id, req, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).RequireAuthorization(AuthorizationPolicies.Technician);

// --- ITAM & CMMS Asset Management Endpoints ---
v1.MapGet("/devices/{id:guid}/asset-detail", async (Guid id, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    if (http.User.ToActorContext() is not { } actor)
    {
        return Results.Unauthorized();
    }
    var detail = await assetService.GetDeviceAssetDetailAsync(actor, id, ct);
    return detail is null ? Results.NotFound() : Results.Ok(detail);
}).RequireAuthorization();

v1.MapPut("/devices/{id:guid}/asset-profile", async (Guid id, UpdateAssetProfileRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var (status, message) = await assetService.UpdateAssetProfileAsync(actor, id, req, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Ok(new { message }),
        ManagementResultStatus.NotFound => Results.NotFound(new { message }),
        ManagementResultStatus.Forbidden => Results.Forbid(),
        _ => Results.BadRequest(new ProblemDetails { Title = "Invalid profile update", Detail = message })
    };
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapGet("/devices/{id:guid}/timeline", async (Guid id, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    if (http.User.ToActorContext() is not { } actor)
    {
        return Results.Unauthorized();
    }
    var timeline = await assetService.GetDeviceTimelineAsync(actor, id, ct);
    return Results.Ok(timeline);
}).RequireAuthorization();

v1.MapGet("/incidents", async (Guid? deviceId, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    if (http.User.ToActorContext() is not { } actor)
    {
        return Results.Unauthorized();
    }
    var incidents = await assetService.GetIncidentsAsync(actor, deviceId, ct);
    return Results.Ok(incidents);
}).RequireAuthorization();

v1.MapPost("/incidents", async (CreateIncidentRequest req, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    if (http.User.ToActorContext() is not { } actor)
    {
        return Results.Unauthorized();
    }
    var (status, incident, message) = await assetService.CreateIncidentAsync(actor, req with { IdempotencyKey = idempotencyKey }, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Created($"/api/v1/incidents/{incident!.Id}", incident),
        ManagementResultStatus.NotFound => Results.NotFound(new { message }),
        ManagementResultStatus.Forbidden => Results.Forbid(),
        ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Idempotency conflict", Detail = message, Status = 409 }),
        _ => Results.BadRequest(new ProblemDetails { Title = "Failed to report incident", Detail = message })
    };
}).RequireAuthorization();

v1.MapPut("/incidents/{id:guid}/status", async (Guid id, UpdateIncidentStatusRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var (status, message) = await assetService.UpdateIncidentStatusAsync(actor, id, req, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Ok(new { message }),
        ManagementResultStatus.NotFound => Results.NotFound(new { message }),
        ManagementResultStatus.Forbidden => Results.Forbid(),
        _ => Results.BadRequest(new ProblemDetails { Title = "Failed to update incident", Detail = message })
    };
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapGet("/work-orders", async (Guid? deviceId, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    if (http.User.ToActorContext() is not { } actor)
    {
        return Results.Unauthorized();
    }
    var workOrders = await assetService.GetWorkOrdersAsync(actor, deviceId, ct);
    return Results.Ok(workOrders);
}).RequireAuthorization();

v1.MapPost("/work-orders", async (CreateWorkOrderRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var (status, wo, message) = await assetService.CreateWorkOrderAsync(actor, req, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Created($"/api/v1/work-orders/{wo!.Id}", wo),
        ManagementResultStatus.NotFound => Results.NotFound(new { message }),
        ManagementResultStatus.Forbidden => Results.Forbid(),
        _ => Results.BadRequest(new ProblemDetails { Title = "Failed to create work order", Detail = message })
    };
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapPut("/work-orders/{id:guid}/complete", async (Guid id, CompleteWorkOrderRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var (status, message) = await assetService.CompleteWorkOrderAsync(actor, id, req, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Ok(new { message }),
        ManagementResultStatus.NotFound => Results.NotFound(new { message }),
        ManagementResultStatus.Forbidden => Results.Forbid(),
        _ => Results.BadRequest(new ProblemDetails { Title = "Failed to complete work order", Detail = message })
    };
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapGet("/asset-loans", async (Guid? deviceId, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var loans = await assetService.GetAssetLoansAsync(actor, deviceId, ct);
    return Results.Ok(loans);
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapPost("/asset-loans", async (CreateLoanRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var (status, loan, message) = await assetService.CreateAssetLoanAsync(actor, req, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Created($"/api/v1/asset-loans/{loan!.Id}", loan),
        ManagementResultStatus.NotFound => Results.NotFound(new { message }),
        ManagementResultStatus.Forbidden => Results.Forbid(),
        _ => Results.BadRequest(new ProblemDetails { Title = "Failed to create asset loan", Detail = message })
    };
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapPut("/asset-loans/{id:guid}/return", async (Guid id, ReturnLoanRequest req, HttpContext http, IAssetManagementService assetService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var (status, message) = await assetService.ReturnAssetLoanAsync(actor, id, req, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Ok(new { message }),
        ManagementResultStatus.NotFound => Results.NotFound(new { message }),
        ManagementResultStatus.Forbidden => Results.Forbid(),
        _ => Results.BadRequest(new ProblemDetails { Title = "Failed to process asset return", Detail = message })
    };
}).RequireAuthorization(AuthorizationPolicies.Technician);

// --- Device QR Label Lifecycle & Scoped Resolution Endpoints ---
v1.MapPost("/devices/{id:guid}/qr-label", async (Guid id, GenerateQrLabelRequest req, HttpContext http, SentinelDbContext db, IQrManagementService qrService, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var actor = http.User.ToActorContext()!.Value;
    var (status, label, message) = await qrService.GenerateOrRotateLabelAsync(actor, id, req, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Created($"/api/v1/devices/{id}/qr-label", label),
        ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "Device not found", Detail = message, Status = 404 }),
        ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        ManagementResultStatus.Conflict => Results.Conflict(new ProblemDetails { Title = "Conflict", Detail = message, Status = 409 }),
        _ => Results.BadRequest(new ProblemDetails { Title = "Invalid request", Detail = message, Status = 400 })
    };
}).RequireAuthorization(AuthorizationPolicies.Technician).RequireRateLimiting("sensitive");

v1.MapDelete("/devices/{id:guid}/qr-label", async (Guid id, [FromBody] RevokeQrLabelRequest? req, HttpContext http, SentinelDbContext db, IQrManagementService qrService, CancellationToken ct) =>
{
    using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
    var actor = http.User.ToActorContext()!.Value;
    var request = req ?? new RevokeQrLabelRequest("Revoked by technician", true);
    var (status, message) = await qrService.RevokeLabelAsync(actor, id, request, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.NoContent(),
        ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "Not found", Detail = message, Status = 404 }),
        ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        _ => Results.BadRequest(new ProblemDetails { Title = "Invalid request", Detail = message, Status = 400 })
    };
}).RequireAuthorization(AuthorizationPolicies.Technician).RequireRateLimiting("sensitive");

v1.MapGet("/devices/{id:guid}/qr-label", async (Guid id, HttpContext http, IQrManagementService qrService, CancellationToken ct) =>
{
    var actor = http.User.ToActorContext()!.Value;
    var label = await qrService.GetActiveLabelAsync(actor, id, ct);
    return label is null ? Results.NotFound() : Results.Ok(label);
}).RequireAuthorization(AuthorizationPolicies.Technician);

v1.MapGet("/qr/{code}/public", async (string code, IQrManagementService qrService, HttpContext http, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var info = await qrService.ResolvePublicAsync(code, ct);
    return info is null ? Results.NotFound() : Results.Ok(info);
}).AllowAnonymous().RequireRateLimiting("sensitive");

v1.MapGet("/qr/{code}", async (string code, HttpContext http, IQrManagementService qrService, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    http.Response.Headers.Pragma = "no-cache";
    var actor = http.User.ToActorContext()!.Value;
    var (status, result, message) = await qrService.ResolveAuthenticatedAsync(actor, code, ct);
    return status switch
    {
        ManagementResultStatus.Succeeded => Results.Ok(result),
        ManagementResultStatus.NotFound => Results.NotFound(new ProblemDetails { Title = "QR not found", Detail = message, Status = 404 }),
        ManagementResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        _ => Results.BadRequest(new ProblemDetails { Title = "Invalid scan", Detail = message, Status = 400 })
    };
}).RequireAuthorization().RequireRateLimiting("sensitive");

app.MapHub<UpdatesHub>("/hubs/updates").RequireAuthorization(AuthorizationPolicies.ViewDevices);

static bool IsUnsafeProductionSecret(string secret) =>
    secret.Length < 32 || new[] { "development-", "local-", "change-me", "replace-", "demo-" }
        .Any(marker => secret.Contains(marker, StringComparison.OrdinalIgnoreCase));

app.Run();

static async Task<IResult> PublishDeviceUpdateAsync(DeviceDto device, IHubContext<UpdatesHub> hub, HttpContext http, CancellationToken cancellationToken)
{
    var actor = http.User.ToActorContext()!.Value;
    await hub.Clients.Group(TenantGroup.Name(actor.OrganizationId)).SendAsync("device-status", device, cancellationToken);
    return Results.Ok(device);
}

public partial class Program;
