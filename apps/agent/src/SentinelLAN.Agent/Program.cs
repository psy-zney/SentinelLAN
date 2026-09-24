using SentinelLAN.Agent;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options => options.ServiceName = "SentinelLAN Agent");
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
builder.Services.AddSingleton<ResilientOfflineQueue<QueuedTelemetry>>();
builder.Services.AddSingleton<ITelemetryCollector, SystemTelemetryCollector>();
builder.Services.AddSingleton<CommandVerifier>();
builder.Services.AddSingleton<ICommandSignatureVerifier>(new HmacCommandVerifier(builder.Configuration["SENTINELLAN_SIGNING_KEY"]));
builder.Services.AddSingleton<IAgentApi>(new AgentApi(new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(20) }, Environment.MachineName));
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();
