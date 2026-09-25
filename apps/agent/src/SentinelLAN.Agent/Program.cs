using SentinelLAN.Agent;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options => options.ServiceName = "SentinelLAN Agent");
    if (builder.Environment.IsProduction())
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SentinelLAN", "Agent", "agent-settings.json");
        builder.Configuration.AddJsonFile(settingsPath, optional: true, reloadOnChange: false);
    }
}

var baseUrl = builder.Configuration["SENTINELLAN_API_URL"] ?? "http://localhost:8080";
var dataDir = builder.Configuration["SENTINELLAN_AGENT_DATA_DIR"] ??
    (builder.Environment.IsProduction()
        ? OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SentinelLAN", "Agent")
            : "/var/lib/sentinellan-agent"
        : Path.Combine(AppContext.BaseDirectory, "agent-data"));
var identityStore = builder.Environment.IsProduction()
    ? (IDeviceIdentityStore)new ProtectedDeviceIdentityStore(Path.Combine(dataDir, "identity.dat"), builder.Configuration["SENTINELLAN_AGENT_STORE_KEY"])
    : new DevelopmentIdentityStore(Path.Combine(dataDir, "identity.json"));
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
builder.Services.AddSingleton<CommandVerifier>();
builder.Services.AddSingleton<ICommandSignatureVerifier>(new HmacCommandVerifier(builder.Configuration["SENTINELLAN_SIGNING_KEY"]));
builder.Services.AddSingleton<IAgentApi>(new AgentApi(new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(20) }, Environment.MachineName));
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();
