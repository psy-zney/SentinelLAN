using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public static class WebApplicationExtensions
{
    public static WebApplication UseSentinelLan(this WebApplication app)
    {
        app.UseForwardedHeaders(ForwardingOptions(app.Configuration));
        app.UseExceptionHandler();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
                context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            await next();
        });
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Correlation-ID"] = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            await next();
        });
        app.UseCors();
        app.Use(async (context, next) =>
        {
            await next();
            if (!HttpMethods.IsGet(context.Request.Method) && context.Response.StatusCode < 400 &&
                (context.Request.Path.StartsWithSegments("/api/v1/users") || context.Request.Path.StartsWithSegments("/api/v1/auth")))
            {
                var connections = context.RequestServices.GetRequiredService<RealtimeConnectionGuard>();
                try { await connections.RevalidateAsync(CancellationToken.None); }
                catch (Exception exception)
                {
                    connections.AbortAll();
                    app.Logger.LogError(exception, "Realtime authorization unavailable after account update; connections closed");
                }
            }
        });
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
        return app;
    }

    private static ForwardedHeadersOptions ForwardingOptions(IConfiguration configuration)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
            RequireHeaderSymmetry = true
        };
        options.KnownProxies.Clear();
        // .NET 10 replaces the obsolete KnownNetworks property with KnownIPNetworks.
        options.KnownIPNetworks.Clear();
        foreach (var value in (configuration["SENTINELLAN_KNOWN_PROXIES"] ?? "127.0.0.1,::1")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IPAddress.TryParse(value, out var address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("SENTINELLAN_KNOWN_PROXIES must contain explicit proxy IP addresses.");
            options.KnownProxies.Add(address);
        }
        foreach (var value in (configuration["SENTINELLAN_KNOWN_NETWORKS"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!System.Net.IPNetwork.TryParse(value, out var network) || network.PrefixLength == 0)
                throw new InvalidOperationException("SENTINELLAN_KNOWN_NETWORKS must contain bounded proxy CIDR networks.");
            options.KnownIPNetworks.Add(network);
        }
        if (options.KnownProxies.Count == 0 && options.KnownIPNetworks.Count == 0)
            throw new InvalidOperationException("At least one trusted proxy or proxy network must be configured.");
        return options;
    }

    public static async Task InitializeSentinelLanAsync(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            if (app.Environment.IsDevelopment())
                await DemoSeeder.SeedAsync(db, passwordHasher, key => app.Configuration[key]);
            else
            {
                await db.Database.MigrateAsync();
                await BootstrapOrganizationInitializer.EnsureCreatedAsync(db, passwordHasher,
                    app.Configuration["SENTINELLAN_BOOTSTRAP_ORG_CODE"],
                    app.Configuration["SENTINELLAN_BOOTSTRAP_ORG_NAME"],
                    app.Configuration["SENTINELLAN_BOOTSTRAP_ADMIN_EMAIL"],
                    app.Configuration["SENTINELLAN_BOOTSTRAP_ADMIN_PASSWORD"]);
            }
            await SensitiveDataInitializer.EncryptLegacyDataAsync(db);
        }
    }
}
