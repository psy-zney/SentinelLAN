using SentinelLAN.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSentinelLan(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseSentinelLan();
await app.InitializeSentinelLanAsync();
app.MapSentinelLanEndpoints();
app.Run();

public partial class Program;
