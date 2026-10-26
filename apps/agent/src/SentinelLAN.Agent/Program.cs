using SentinelLAN.Agent;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;
using System.Reflection;
using System.Text.Json;

if (args.Contains("--update-agent", StringComparer.Ordinal))
{
    try { Environment.ExitCode = await AgentUpdateRunner.RunAsync(CancellationToken.None); }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.Cryptography.CryptographicException or HttpRequestException or OperationCanceledException or JsonException)
    {
        Console.Error.WriteLine("Agent update could not complete. IT must inspect the protected update status and Windows Installer events.");
        Environment.ExitCode = 1;
    }
    return;
}

var desktopCompanion = args.Contains("--desktop-companion", StringComparer.Ordinal);
var maintenanceDialog = args.Contains("--maintenance", StringComparer.Ordinal);
if (maintenanceDialog)
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Maintenance popup requires Windows.");
    await WindowsMaintenanceBridge.ShowDialogAsync(CancellationToken.None);
    return;
}
var checkConfiguration = args.Contains("--check-configuration", StringComparer.Ordinal);
var hostArguments = args.Where(argument => argument is not ("--desktop-companion" or "--check-configuration")).ToArray();
var builder = Host.CreateApplicationBuilder(hostArguments);
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options => options.ServiceName = "SentinelLANAgent");
    if (builder.Environment.IsProduction() && !desktopCompanion)
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SentinelLAN", "Agent", "agent-settings.json");
        builder.Configuration.AddAgentSettings(settingsPath, hostArguments);
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

string? Metadata(string key) => Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
    .SingleOrDefault(attribute => attribute.Key == key)?.Value is { Length: > 0 } value ? value : null;
var companyServer = Metadata("CompanyServerUrl");
var apiUrl = AgentServerConfiguration.Resolve(builder.Configuration["SENTINELLAN_API_URL"], companyServer, builder.Environment.IsDevelopment());
var commandPublicKey = Metadata("CommandPublicKey") ?? builder.Configuration["SENTINELLAN_COMMAND_PUBLIC_KEY"];
var commandKeyId = Metadata("CommandKeyId") ?? builder.Configuration["SENTINELLAN_COMMAND_KEY_ID"];
if (companyServer is not null && (string.IsNullOrWhiteSpace(commandPublicKey) || string.IsNullOrWhiteSpace(commandKeyId)))
    throw new InvalidOperationException("The company Agent is missing its command verification key. Obtain a complete company installer.");
if (checkConfiguration)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        ServerUrl = apiUrl.AbsoluteUri.TrimEnd('/'),
        CompanyPinned = companyServer is not null,
        CommandKeyId = commandKeyId,
        HasCommandPublicKey = !string.IsNullOrWhiteSpace(commandPublicKey),
        CommandPublicKeySha256 = string.IsNullOrWhiteSpace(commandPublicKey) ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Convert.FromBase64String(commandPublicKey))).ToLowerInvariant(),
        SourceRevision = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+').ElementAtOrDefault(1),
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    }));
    return;
}

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
builder.Services.AddSingleton<IAgentEnrollmentProgress>(new FileAgentEnrollmentProgress(Path.Combine(dataDir, "enrollment-status.json")));
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
var observations = new EndpointObservationCollector(new EndpointPlatformProbe());
builder.Services.AddSingleton<IEndpointObservationCollector>(observations);
builder.Services.AddSingleton<IHardwareInventoryCollector>(observations);
var msiPlatform = new WindowsMsiOperations();
builder.Services.AddSingleton<ICommandExecutor>(OperatingSystem.IsLinux() ? new LinuxCommandExecutor(executionOptions, apiUrl, new LinuxCommandPlatform()) : new WindowsCommandExecutor(executionOptions, apiUrl,
    new ApprovedAppInstaller(executionOptions, Path.Combine(dataDir, "packages"), msiPlatform),
    new AgentMaintenanceExecutor(executionOptions, maintenanceStore, msiPlatform)));
builder.Services.AddSingleton<IAgentPolicyApplier>(new WindowsPolicyApplier(executionOptions));
builder.Services.AddSingleton<CommandVerifier>();
builder.Services.AddSingleton<ICommandSignatureVerifier>(string.IsNullOrWhiteSpace(commandPublicKey)
    ? new HmacCommandVerifier(builder.Configuration["SENTINELLAN_SIGNING_KEY"])
    : new RsaCommandVerifier(commandPublicKey, commandKeyId ?? ""));
var enrollmentAttemptPath = Path.Combine(dataDir, "enrollment-attempt.dat");
var enrollmentAttempts = new FileEnrollmentAttemptStore(enrollmentAttemptPath,
    builder.Environment.IsProduction()
        ? new ProtectedDeviceIdentityStore(enrollmentAttemptPath, builder.Configuration["SENTINELLAN_AGENT_STORE_KEY"])
        : new DevelopmentIdentityStore(enrollmentAttemptPath));
var agentApi = new AgentApi(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = apiUrl, Timeout = TimeSpan.FromSeconds(20) }, Environment.MachineName, enrollmentAttempts, observations);
builder.Services.AddSingleton<IAgentApi>(agentApi);
builder.Services.AddSingleton<IAgentWakeSignal>(new AgentWakeSignal(apiUrl));
builder.Services.AddSingleton<ISoftwareObservationCollector>(new SoftwareObservationCollector(agentApi, new SoftwareProductProbe()));
builder.Services.AddHostedService<Worker>();
if (OperatingSystem.IsWindows()) builder.Services.AddHostedService<MaintenanceBridgeWorker>();
await builder.Build().RunAsync();
