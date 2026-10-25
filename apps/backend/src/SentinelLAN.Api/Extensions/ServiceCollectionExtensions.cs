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
        // Invalid or missing JSON bodies are client errors in every environment.
        // Throwing in Development would let the exception middleware turn them into 500s.
        services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 4 * 1024 * 1024;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<AuthenticationOpenApiTransformer>();
            options.AddOperationTransformer<AuthenticationOpenApiTransformer>();
        });
        var realtime = services.AddSignalR();
        if (!string.IsNullOrWhiteSpace(configuration["SENTINELLAN_REDIS_CONNECTION"]))
        {
            var prefix = configuration["SENTINELLAN_REDIS_CHANNEL_PREFIX"];
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 80 || prefix.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
                throw new InvalidOperationException("Redis requires a unique SENTINELLAN_REDIS_CHANNEL_PREFIX (letters, digits, dash, underscore).");
            realtime.AddStackExchangeRedis(configuration["SENTINELLAN_REDIS_CONNECTION"]!, options => options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal(prefix));
        }
        services.AddSingleton<AgentWakeConnectionGuard>();
        services.AddHostedService<OperationalOutboxWorker>();
        services.AddScoped<IOperationalNotificationSink>(provider => provider.GetRequiredService<SelfServiceService>());
        services.AddSingleton<RealtimeConnectionGuard>();
        services.AddHostedService<RealtimeAuthorizationWorker>();
        services.AddHostedService<DeviceStatusPublisher>();
        var allowedOrigins = (configuration["SENTINELLAN_WEB_ORIGINS"] ?? "http://localhost:3000,http://localhost:3001")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!environment.IsDevelopment() && (allowedOrigins.Length == 0 || allowedOrigins.Any(origin =>
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 ||
            uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)))
            throw new InvalidOperationException("SENTINELLAN_WEB_ORIGINS must contain explicit HTTPS origins outside Development.");
        services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
                    ? Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture) : "1";
                return ValueTask.CompletedTask;
            };
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var agent = context.User.ToAgentContext();
                var actor = context.User.ToActorContext();
                var key = agent is { } device ? $"device:{device.OrganizationId}:{device.DeviceId}"
                    : actor is { } user ? $"user:{user.OrganizationId}:{user.UserId}" : null;
                return key is null ? RateLimitPartition.GetNoLimiter("anonymous") : RateLimitPartition.GetTokenBucketLimiter(key,
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 300,
                        TokensPerPeriod = 300,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        AutoReplenishment = true,
                        QueueLimit = 0
                    });
            });
            options.AddPolicy("sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
                $"{(context.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText?.ToLowerInvariant() ?? "unknown"}|{context.User.ToAgentContext()?.DeviceId.ToString() ?? context.User.ToActorContext()?.UserId.ToString() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
        services.AddSingleton<IngressRequestBudget>();
        services.AddScoped<ILoginAttemptLimiter, LoginAttemptLimiter>();

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
        var dataEncryptionKey = configuration["SENTINELLAN_DATA_ENCRYPTION_KEY"];
        var developmentDataKey = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("development-data-key-never-use-in-production")));
        if (string.IsNullOrWhiteSpace(dataEncryptionKey) && !environment.IsDevelopment())
            throw new InvalidOperationException("SENTINELLAN_DATA_ENCRYPTION_KEY is required outside Development.");
        dataEncryptionKey ??= developmentDataKey;
        if (!environment.IsDevelopment() && dataEncryptionKey == developmentDataKey)
            throw new InvalidOperationException("The development data encryption key cannot be used in production.");
        if (dataEncryptionKey == accessTokenSigningKey || dataEncryptionKey == serverVaultKey || dataEncryptionKey == signingKey)
            throw new InvalidOperationException("The data encryption key must be distinct from authentication and command keys.");
        services.AddSingleton(new SensitiveDataCipher(dataEncryptionKey));
        var attachmentMode = configuration["SENTINELLAN_ATTACHMENT_STORAGE"] ?? "Database";
        if (attachmentMode is not ("Database" or "S3")) throw new InvalidOperationException("SENTINELLAN_ATTACHMENT_STORAGE must be Database or S3.");
        services.AddSingleton(new AttachmentObjectStoreOptions(attachmentMode == "S3", configuration["SENTINELLAN_S3_ENDPOINT"], configuration["SENTINELLAN_S3_REGION"],
            configuration["SENTINELLAN_S3_BUCKET"], configuration["SENTINELLAN_S3_ACCESS_KEY"], configuration["SENTINELLAN_S3_SECRET_KEY"], configuration.GetValue<bool>("SENTINELLAN_S3_FORCE_PATH_STYLE")));
        services.AddSingleton<IAttachmentObjectStore, AttachmentObjectStore>();
        services.AddHostedService<AttachmentOffloadWorker>();
        var privateCommandKey = configuration["SENTINELLAN_COMMAND_PRIVATE_KEY_PEM"];
        var useRsaCommands = !string.IsNullOrWhiteSpace(privateCommandKey);
        if (!environment.IsDevelopment() && !useRsaCommands)
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
        DateTimeOffset? legacyPasswordUntil = null;
        if (configuration["SENTINELLAN_LEGACY_PASSWORD_HASHES_UNTIL"] is { Length: > 0 } legacyValue)
        {
            if (!DateTimeOffset.TryParse(legacyValue, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var until) ||
                until > DateTimeOffset.UtcNow.AddDays(7))
                throw new InvalidOperationException("Legacy password verification expiry must be a UTC timestamp no more than seven days ahead.");
            legacyPasswordUntil = until;
        }
        services.AddSingleton<IPasswordHasher>(_ => new Pbkdf2PasswordHasher(legacyUntil: legacyPasswordUntil, allowLegacySha256: environment.IsDevelopment()));
        services.AddSingleton<IRefreshTokenProtector, RefreshTokenProtector>();
        var loginOrganizationCode = (configuration["SENTINELLAN_LOGIN_ORG_CODE"] ?? configuration["SENTINELLAN_BOOTSTRAP_ORG_CODE"] ??
            (environment.IsDevelopment() ? "demo" : "")).Trim().ToLowerInvariant();
        if (loginOrganizationCode.Length is < 3 or > 50 || loginOrganizationCode[0] is < 'a' or > 'z' ||
            !loginOrganizationCode.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            throw new InvalidOperationException("Configure SENTINELLAN_LOGIN_ORG_CODE for the single company hosted by this server.");
        services.AddSingleton(new AuthenticationSettings(TimeSpan.FromMinutes(15), TimeSpan.FromDays(7), loginOrganizationCode));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(AgentRequestAuthenticationSettings.FromConfiguration(configuration, environment));
        services.AddScoped<AgentCredentialProvisioner>();
        services.AddScoped<IMonitoringSettingsStore, MonitoringSettingsStore>();
        services.AddScoped<MonitoringSettingsService>();
        services.AddScoped<IAdministrativeRequestJournal, AdministrativeRequestJournal>();
        services.AddScoped<ISoftwareCatalogDetectionReader, SoftwareCatalogDetectionReader>();
        services.AddScoped<ISoftwareComplianceStore, SoftwareComplianceStore>();
        services.AddScoped<ISoftwareDetectionQuota, SoftwareDetectionQuota>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<EnrollmentBatchService>();
        services.AddScoped<IDepartmentStore, DepartmentStore>();
        services.AddScoped<IDepartmentPolicyReader, DepartmentStore>();
        services.AddScoped<SoftwareComplianceService>();
        services.AddScoped<IPreventiveDeviceReader, PreventiveDeviceReader>();
        services.AddScoped<IPreventiveMaintenanceStore, PreventiveMaintenanceStore>();
        services.AddScoped<PreventiveMaintenanceService>();
        services.AddScoped<IEndpointObservationReader, EndpointObservationReader>();
        services.AddScoped<EndpointObservationService>();
        services.AddSingleton<MonitoringChangeSignal>();
        services.AddSingleton<IMonitoringChangeSignal>(provider => provider.GetRequiredService<MonitoringChangeSignal>());
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantProvider, HttpCurrentTenantProvider>();
        services.AddScoped<IAuthenticationStore, AuthenticationStore>();
        services.AddSingleton(new VpsHostMonitorSettings(configuration["SENTINELLAN_HOST_MONITOR_ORG_CODE"]?.Trim()));
        services.AddSingleton<IVpsHostSnapshotReader>(new FileVpsHostSnapshotReader(configuration["SENTINELLAN_HOST_SNAPSHOT_PATH"]));
        services.AddScoped<VpsHostMonitorService>();
        services.AddScoped<IRealtimeSessionValidator, RealtimeSessionValidator>();
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
        services.AddScoped<IDeviceHealthReader, DeviceHealthReader>();
        services.AddScoped<IAutomaticAlertStore, AutomaticAlertStore>();
        services.AddScoped<DeviceHealthMonitoringService>();
        services.AddScoped<ITechnicalDataRetentionStore, TechnicalDataRetentionStore>();
        services.AddScoped<TechnicalDataRetentionService>();
        services.AddHostedService<DeviceHealthWorker>();
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
            options.AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.Employee, policy => policy.RequireRole(Roles.Employee));
            options.AddPolicy(AuthorizationPolicies.Agent, policy =>
            {
                policy.AddAuthenticationSchemes(AgentAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(Roles.Agent);
            });
            options.AddPolicy(AuthorizationPolicies.ViewDevices, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.ViewAssignedDevice, policy => policy.RequireRole(Roles.Employee));
            options.AddPolicy(AuthorizationPolicies.ManageCommands, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.ViewAudit, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.ViewPolicies, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.ManagePolicies, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.ViewAlerts, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.ManageAlerts, policy => policy.RequireRole(Roles.Admin));
        });
        return services;
    }

    private static bool IsUnsafeProductionSecret(string secret) =>
        secret.Length < 32 || new[] { "development-", "local-", "change-me", "replace-", "demo-" }
            .Any(marker => secret.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
