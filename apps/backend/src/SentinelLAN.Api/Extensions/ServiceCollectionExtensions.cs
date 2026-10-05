using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSentinelLan(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var deployment = DeploymentSettings.FromConfiguration(configuration);
        services.AddSingleton(deployment);
        services.AddProblemDetails();
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<AuthenticationOpenApiTransformer>();
            options.AddOperationTransformer<AuthenticationOpenApiTransformer>();
        });
        services.AddSignalR();
        services.AddHostedService<DeviceStatusPublisher>();
        var allowedOrigins = (configuration["SENTINELLAN_WEB_ORIGINS"] ?? "http://localhost:3000,http://localhost:3001,http://localhost:3002")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
                $"{context.Request.Path}|{context.User.ToAgentContext()?.DeviceId.ToString() ?? context.User.ToActorContext()?.UserId.ToString() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });

        var connectionString = configuration.GetConnectionString("SentinelLAN");
        if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings__SentinelLAN is required outside Development.");
        services.AddDbContext<SentinelDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString)) options.UseInMemoryDatabase("sentinellan-development");
            else options.UseNpgsql(connectionString);
        });
        var signingKey = configuration["SENTINELLAN_SIGNING_KEY"] ?? "development-signing-key-change-before-deployment";
        var accessTokenSigningKey = configuration["SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY"] ?? "development-access-token-signing-key-change-before-deployment";
        var serverVaultKey = configuration["SENTINELLAN_SERVER_VAULT_KEY"] ?? "development-server-vault-key-32-bytes-long!";
        var privateCommandKey = configuration["SENTINELLAN_COMMAND_PRIVATE_KEY_PEM"];
        var useRsaCommands = !string.IsNullOrWhiteSpace(privateCommandKey);
        if (!deployment.PlatformEnabled && !useRsaCommands)
            throw new InvalidOperationException("SelfHost requires SENTINELLAN_COMMAND_PRIVATE_KEY_PEM and a company installer with the corresponding public key.");
        if (!environment.IsDevelopment())
        {
            if (!useRsaCommands && IsUnsafeProductionSecret(signingKey))
                throw new InvalidOperationException("SENTINELLAN_SIGNING_KEY must be a unique secret of at least 32 characters outside Development.");
            if (IsUnsafeProductionSecret(accessTokenSigningKey))
                throw new InvalidOperationException("SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY must be set outside Development.");
            if (IsUnsafeProductionSecret(serverVaultKey))
                throw new InvalidOperationException("SENTINELLAN_SERVER_VAULT_KEY must be a unique secret of at least 32 characters outside Development.");
            if (string.Equals(signingKey, accessTokenSigningKey, StringComparison.Ordinal) ||
                string.Equals(signingKey, serverVaultKey, StringComparison.Ordinal) ||
                string.Equals(accessTokenSigningKey, serverVaultKey, StringComparison.Ordinal))
                throw new InvalidOperationException("Command signing, access-token signing, and server vault keys must be distinct outside Development.");
        }
        if (accessTokenSigningKey.Length < 32) throw new InvalidOperationException("The access-token signing key must contain at least 32 characters.");
        if (useRsaCommands)
        {
            // Validate before startup succeeds; the container owns and disposes its singleton.
            var keyId = configuration["SENTINELLAN_COMMAND_KEY_ID"] ?? "";
            using (var validation = new RsaCommandSigner(privateCommandKey!, keyId)) { }
            services.AddSingleton<ICommandSigner>(_ => new RsaCommandSigner(privateCommandKey!, keyId));
        }
        else services.AddSingleton<ICommandSigner>(new HmacCommandSigner(signingKey));
        services.AddSingleton<IAccessTokenService>(new AccessTokenService(accessTokenSigningKey));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IRefreshTokenProtector, RefreshTokenProtector>();
        services.AddSingleton(new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7)));
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantProvider, HttpCurrentTenantProvider>();
        services.AddScoped<IAuthenticationStore, AuthenticationStore>();
        if (deployment.PlatformEnabled)
        {
            services.AddScoped<IPlatformStore, PlatformStore>();
            services.AddScoped<PlatformService>();
        }
        services.AddScoped<SentinelLAN.Application.AuthenticationService>();
        services.AddScoped<IManagementStore, ManagementStore>();
        services.AddSingleton<IEnrollmentSecretGenerator, EnrollmentSecretGenerator>();
        services.AddSingleton<ISecretHasher, SecretHasher>();
        services.AddScoped<UserManagementService>();
        services.AddScoped<EnrollmentTokenService>();
        services.AddScoped<DeviceManagementService>();
        services.AddScoped<IPolicyStore, PolicyStore>();
        services.AddScoped<PolicyService>();
        services.AddScoped<IAlertStore, AlertStore>();
        services.AddScoped<IAlertDeviceLookup, AlertDeviceLookup>();
        services.AddScoped<AlertService>();
        if (deployment.PlatformEnabled)
        {
            services.AddSingleton<IVpsVaultService>(new VpsVaultService(serverVaultKey));
            services.AddSingleton<IVpsSshService, SshNetVpsSshService>();
            services.AddScoped<IVpsNodeStore, VpsNodeStore>();
            services.AddScoped<VpsNodeService>();
            services.AddSingleton<IVpsHostSnapshotReader>(new FileVpsHostSnapshotReader(configuration["SENTINELLAN_HOST_MONITOR_PATH"]));
            services.AddScoped<VpsHostMonitorService>();
        }
        services.AddScoped<IAssetStore, AssetStore>();
        services.AddScoped<IAssetManagementService, AssetManagementService>();
        services.AddSingleton<IActivationTokenGenerator, ActivationTokenGenerator>();
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        services.AddScoped<IQrManagementService, QrManagementService>();
        services.AddScoped<IMyDeviceStore, MyDeviceStore>();
        services.AddScoped<IMyDeviceService, MyDeviceService>();
        services.AddSelfService(configuration, serverVaultKey);
        services.AddSingleton<AuthCookieManager>();
        services
            .AddAuthentication(SentinelAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, AccessTokenAuthenticationHandler>(SentinelAuthenticationDefaults.Scheme, _ => { })
            .AddScheme<AuthenticationSchemeOptions, AgentAuthenticationHandler>(AgentAuthenticationDefaults.Scheme, _ => { });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Roles.PlatformOwner, policy => policy.RequireRole(Roles.PlatformOwner));
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
        return services;
    }

    private static bool IsUnsafeProductionSecret(string secret) =>
        secret.Length < 32 || new[] { "development-", "local-", "change-me", "replace-", "demo-" }
            .Any(marker => secret.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
