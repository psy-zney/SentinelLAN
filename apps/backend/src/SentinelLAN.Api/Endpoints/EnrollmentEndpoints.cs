using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Enrollment;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api.Endpoints;

public static class EnrollmentEndpoints
{
    public static void MapEnrollmentEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapPost("/enrollment-packages", async (EnrollmentBatchRequest request, HttpContext http, IConfiguration config, EnrollmentBatchService service, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (request.Count is < 1 or > 50 || request.Platform is not ("Windows" or "Linux")) return Results.BadRequest();
            try
            {
                var settings = EnrollmentBootstrap.Read(config, request.Platform);
                var tokens = await service.CreateAsync(http.User.ToActorContext()!.Value, request, ct);
                return Results.Ok(tokens.Select((token, index) => EnrollmentBootstrap.Create(settings, token.Token, token.ExpiresAt, request.Platform, index + 1)).ToArray());
            }
            catch (OrganizationConfigurationException error) { return Results.Problem(error.Message, statusCode: error.StatusCode); }
            catch (Exception error) when (error is FormatException or ArgumentException or System.Security.Cryptography.CryptographicException or InvalidOperationException)
            { return Results.Problem("IT cần cấu hình địa chỉ máy chủ, khóa RSA và bản phát hành được kiểm chứng trước khi cấp gói cài.", statusCode: 503); }
        }).RequireAuthorization(AuthorizationPolicies.Admin).RequireRateLimiting("sensitive").Produces<IReadOnlyList<EnrollmentBootstrapPackage>>();
        v1.MapPost("/enrollment-tokens", async (EnrollmentTokenRequest request, HttpContext http, SentinelDbContext db, EnrollmentTokenService tokens, IConfiguration configuration, IHostEnvironment environment, CancellationToken ct) =>
        {
            // Never derive the bootstrap destination from an untrusted Host/Forwarded header.
            var configuredUrl = configuration["SENTINELLAN_AGENT_PUBLIC_URL"];
            if (string.IsNullOrWhiteSpace(configuredUrl) && environment.IsDevelopment()) configuredUrl = "http://localhost:8080";
            string serverUrl;
            try { serverUrl = EnrollmentConnectionCode.NormalizeServerUrl(configuredUrl ?? ""); }
            catch (FormatException)
            {
                return Results.Problem("IT cần cấu hình địa chỉ HTTPS của máy chủ trước khi cấp mã kết nối.", statusCode: 503);
            }
            using var developmentWrite = db.Database.IsRelational() ? null : await DevelopmentWriteGate.EnterAsync(ct);
            var actor = http.User.ToActorContext()!.Value;
            var result = await tokens.CreateAsync(actor, request, ct);
            if (result.Status == ManagementResultStatus.Forbidden) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (result.Status != ManagementResultStatus.Succeeded) return Results.BadRequest(new ProblemDetails { Title = "Invalid enrollment token details", Status = 400 });
            http.Response.Headers.CacheControl = "no-store";
            var token = result.Token!;
            return Results.Created("/api/v1/enrollment-tokens", token with
            {
                ConnectionCode = token.Token
            });
        })
            .WithName("CreateEnrollmentToken")
            .WithSummary("Issue a single-use token; SelfHost uses an opaque token and a company-specific installer")
            .Produces<EnrollmentTokenResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .RequireRateLimiting("sensitive");
    }
}
