using SentinelLAN.Agent;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

var desktopCompanion = args.Contains("--desktop-companion", StringComparer.Ordinal);
var maintenanceDialog = args.Contains("--maintenance", StringComparer.Ordinal);
if (maintenanceDialog)
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Maintenance popup requires Windows.");
    await WindowsMaintenanceBridge.ShowDialogAsync(CancellationToken.None);
    return;
}
var builder = Host.CreateApplicationBuilder(args.Where(argument => argument != "--desktop-companion").ToArray());
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options => options.ServiceName = "SentinelLANAgent");
    if (builder.Environment.IsProduction() && !desktopCompanion)
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SentinelLAN", "Agent", "agent-settings.json");
        builder.Configuration.AddJsonFile(settingsPath, optional: true, reloadOnChange: false);
    }
}

var executionOptions = new AgentExecutionOptions(
    LabExecution: bool.TryParse(builder.Configuration["SENTINELLAN_LAB_EXECUTION"], out var labExecution) && labExecution,
    AuthorizedDeviceId: Guid.TryParse(builder.Configuration["SENTINELLAN_LAB_DEVICE_ID"], out var labDeviceId) ? labDeviceId : Guid.Empty,
    AllowServiceRestart: bool.TryParse(builder.Configuration["SENTINELLAN_ALLOW_SERVICE_RESTART"], out var serviceRestart) && serviceRestart,
    AllowPolicyChanges: bool.TryParse(builder.Configuration["SENTINELLAN_ALLOW_POLICY_CHANGES"], out var policyChanges) && policyChanges,
    IsolationServerIpv4: builder.Configuration["SENTINELLAN_ISOLATION_SERVER_IPV4"],
    AllowApprovedAppInstall: bool.TryParse(builder.Configuration["SENTINELLAN_ALLOW_APPROVED_APP_INSTALL"], out var appInstall) && appInstall,
    AllowAgentMaintenance: bool.TryParse(builder.Configuration["SENTINELLAN_ALLOW_AGENT_MAINTENANCE"], out var maintenance) && maintenance,
    TrustedPackageHosts: builder.Configuration["SENTINELLAN_TRUSTED_PACKAGE_HOSTS"]?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
if (executionOptions.LabExecution && executionOptions.AuthorizedDeviceId == Guid.Empty)
    throw new InvalidOperationException("SENTINELLAN_LAB_EXECUTION requires SENTINELLAN_LAB_DEVICE_ID for an authorized test device.");
if (desktopCompanion)
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Desktop companion requires Windows.");
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    await WindowsDesktopBridge.RunCompanionAsync(executionOptions, cancellation.Token);
    return;
}

var baseUrl = builder.Configuration["SENTINELLAN_API_URL"] ?? "http://localhost:8080";
if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var apiUrl) || apiUrl.Scheme is not ("http" or "https") ||
    (apiUrl.Scheme != "https" && !apiUrl.IsLoopback) || !string.IsNullOrEmpty(apiUrl.UserInfo) ||
    !string.IsNullOrEmpty(apiUrl.Query) || !string.IsNullOrEmpty(apiUrl.Fragment))
    throw new InvalidOperationException("SENTINELLAN_API_URL must use trusted HTTPS outside loopback and contain no credentials, query or fragment.");
var dataDir = builder.Configuration["SENTINELLAN_AGENT_DATA_DIR"] ??
    (builder.Environment.IsProduction()
        ? OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SentinelLAN", "Agent")
            : "/var/lib/sentinellan-agent"
        : Path.Combine(AppContext.BaseDirectory, "agent-data"));
var identityStore = builder.Environment.IsProduction()
    ? (IDeviceIdentityStore)new ProtectedDeviceIdentityStore(Path.Combine(dataDir, "identity.dat"), builder.Configuration["SENTINELLAN_AGENT_STORE_KEY"])
    : new DevelopmentIdentityStore(Path.Combine(dataDir, "identity.json"));
if (OperatingSystem.IsWindows() && builder.Environment.IsProduction() && System.Diagnostics.Process.GetCurrentProcess().SessionId == 0)
    WindowsAgentProtection.ProtectServiceProcess();
var maintenanceStore = builder.Environment.IsProduction()
    ? (IAgentMaintenanceStateStore)new ProtectedMaintenanceStateStore(Path.Combine(dataDir, "maintenance.dat"), builder.Configuration["SENTINELLAN_AGENT_STORE_KEY"])
    : new InMemoryAgentMaintenanceStateStore();
builder.Services.AddSingleton(maintenanceStore);
builder.Services.AddSingleton<IDeviceIdentityStore>(identityStore);
builder.Services.AddSingleton<ICommandNonceStore>(builder.Environment.IsProduction()
    ? new ProtectedCommandNonceStore(Path.Combine(dataDir, "command-nonces.dat"), builder.Configuration["SENTINELLAN_AGENT_STORE_KEY"])
    : new InMemoryCommandNonceStore());
var offlineQueue = builder.Environment.IsProduction()
    ? new ResilientOfflineQueue<QueuedTelemetry>(50,
        new ProtectedOfflineTelemetryQueueStore(Path.Combine(dataDir, "offline-telemetry.dat"), builder.Configuration["SENTINELLAN_AGENT_STORE_KEY"]),
        TimeSpan.FromHours(1))
    : new ResilientOfflineQueue<QueuedTelemetry>();
builder.Services.AddSingleton(offlineQueue);
if (builder.Environment.IsProduction())
    builder.Services.AddSingleton<IPendingCommandResultStore>(new ProtectedPendingCommandResultStore(
        Path.Combine(dataDir, "pending-command-result.dat"), builder.Configuration["SENTINELLAN_AGENT_STORE_KEY"]));
builder.Services.AddSingleton<ITelemetryCollector, SystemTelemetryCollector>();
var msiPlatform = new WindowsMsiOperations();
builder.Services.AddSingleton<ICommandExecutor>(new WindowsCommandExecutor(executionOptions, apiUrl,
    new ApprovedAppInstaller(executionOptions, Path.Combine(dataDir, "packages"), msiPlatform),
    new AgentMaintenanceExecutor(executionOptions, maintenanceStore, msiPlatform)));
builder.Services.AddSingleton<IAgentPolicyApplier>(new WindowsPolicyApplier(executionOptions));
builder.Services.AddSingleton<CommandVerifier>();
builder.Services.AddSingleton<ICommandSignatureVerifier>(new HmacCommandVerifier(builder.Configuration["SENTINELLAN_SIGNING_KEY"]));
builder.Services.AddSingleton<IAgentApi>(new AgentApi(new HttpClient { BaseAddress = apiUrl, Timeout = TimeSpan.FromSeconds(20) }, Environment.MachineName));
builder.Services.AddHostedService<Worker>();
if (OperatingSystem.IsWindows()) builder.Services.AddHostedService<MaintenanceBridgeWorker>();
await builder.Build().RunAsync();
