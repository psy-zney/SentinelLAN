using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public static class WebApplicationExtensions
{
    public static WebApplication UseSentinelLan(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Correlation-ID"] = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            await next();
        });
        app.UseCors();
        app.Use(async (context, next) =>
        {
            var unsafeMethod = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
            var cookieAuthenticated = context.Request.Cookies.ContainsKey(AuthCookieManager.AccessCookieName) || context.Request.Cookies.ContainsKey(AuthCookieManager.RefreshCookieName) ||
                context.Request.Cookies.ContainsKey(PlatformApi.AccessCookie) || context.Request.Cookies.ContainsKey(PlatformApi.RefreshCookie);
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
            await PlatformOwnerInitializer.EnsureCreatedAsync(db, passwordHasher,
                app.Configuration["SENTINELLAN_PLATFORM_OWNER_EMAIL"], app.Configuration["SENTINELLAN_PLATFORM_OWNER_PASSWORD"],
                app.Environment.IsDevelopment());
        }
    }
}
