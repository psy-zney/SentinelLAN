using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Api;

namespace SentinelLAN.IntegrationTests;

public sealed class ForwardedHeadersTests
{
    [Theory]
    [InlineData("192.0.2.10", "192.0.2.10", "", true)]
    [InlineData("192.0.2.11", "192.0.2.10", "", false)]
    [InlineData("198.51.100.20", "", "198.51.100.0/24", true)]
    [InlineData("198.51.101.20", "", "198.51.100.0/24", false)]
    [InlineData("2001:db8::10", "2001:db8::10", "", true)]
    [InlineData("::ffff:192.0.2.10", "192.0.2.10", "", true)]
    [InlineData("127.0.0.1", null, null, true)]
    [InlineData("192.0.2.10", null, null, false)]
    public async Task OnlyTrustedPeersCanSupplyClientAddressAndScheme(string peer, string? proxies, string? networks, bool trusted)
    {
        await using var app = await CreateAsync(proxies, networks);
        var context = await app.GetTestServer().SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            context.Request.Path = "/";
            context.Request.Host = new HostString("sentinel.example.com");
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.25";
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Request.Headers["X-Forwarded-Host"] = "untrusted.example.com";
        });
        Assert.Equal(trusted ? "203.0.113.25" : IPAddress.Parse(peer).ToString(), context.Response.Headers["Observed-IP"].ToString());
        Assert.Equal(trusted ? "https" : "http", context.Response.Headers["Observed-Scheme"].ToString());
        Assert.Equal("sentinel.example.com", context.Response.Headers["Observed-Host"].ToString());
    }

    [Theory]
    [InlineData("198.51.100.1, 203.0.113.25", "http, https", "203.0.113.25", "https")]
    [InlineData("198.51.100.1, 203.0.113.25", "https", "192.0.2.10", "http")]
    public async Task ConsumesOneHopAndRejectsMismatchedHeaderChains(string forwardedFor, string forwardedProto, string ip, string scheme)
    {
        await using var app = await CreateAsync("192.0.2.10", "");
        var context = await app.GetTestServer().SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
            context.Request.Path = "/";
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
            context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        });
        Assert.Equal(ip, context.Response.Headers["Observed-IP"].ToString());
        Assert.Equal(scheme, context.Response.Headers["Observed-Scheme"].ToString());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("invalid", "")]
    [InlineData("0.0.0.0", "")]
    [InlineData("::", "")]
    [InlineData("", "0.0.0.0/0")]
    [InlineData("", "::/0")]
    [InlineData("", "invalid")]
    public async Task InvalidOrUnboundedTrustConfigurationFailsStartup(string proxies, string networks) =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(proxies, networks));

    private static async Task<WebApplication> CreateAsync(string? proxies, string? networks)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SENTINELLAN_KNOWN_PROXIES"] = proxies,
            ["SENTINELLAN_KNOWN_NETWORKS"] = networks
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddCors();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(_ => { });
        builder.Services.AddSingleton<IngressRequestBudget>();
        var app = builder.Build();
        try
        {
            app.UseSentinelLan();
            app.MapGet("/", (HttpContext context) =>
            {
                context.Response.Headers["Observed-IP"] = context.Connection.RemoteIpAddress?.ToString();
                context.Response.Headers["Observed-Scheme"] = context.Request.Scheme;
                context.Response.Headers["Observed-Host"] = context.Request.Host.Value;
                return Task.CompletedTask;
            });
            await app.StartAsync();
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }
}
