using SentinelLAN.Agent;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "SentinelLAN Agent");
var baseUrl = builder.Configuration["SENTINELLAN_API_URL"] ?? "http://localhost:8080";
builder.Services.AddSingleton<IDeviceIdentityStore>(new DevelopmentIdentityStore(Path.Combine(AppContext.BaseDirectory, "agent-data", "identity.json")));
builder.Services.AddSingleton<ITelemetryCollector, SystemTelemetryCollector>();
builder.Services.AddSingleton<CommandVerifier>();
builder.Services.AddSingleton<IAgentApi>(new AgentApi(new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(20) }, Environment.MachineName));
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();
