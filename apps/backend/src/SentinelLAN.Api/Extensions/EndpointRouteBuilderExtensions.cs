using SentinelLAN.Api.Endpoints;
using SentinelLAN.Application;

namespace SentinelLAN.Api;

public static class EndpointRouteBuilderExtensions
{
    public static WebApplication MapSentinelLanEndpoints(this WebApplication app)
    {
        app.MapHealthEndpoints();

        var v1 = app.MapGroup("/api/v1");
        v1.MapSelfService();
        v1.MapAuthEndpoints();
        v1.MapHostMonitoringEndpoints();
        v1.MapMobileAuthEndpoints();
        v1.MapUserEndpoints();
        v1.MapEnrollmentEndpoints();
        v1.MapDeviceEndpoints();
        v1.MapMyDeviceEndpoints();
        v1.MapAgentEndpoints();
        v1.MapCommandEndpoints();
        v1.MapPolicyEndpoints();
        v1.MapAlertEndpoints();
        v1.MapAuditEndpoints();
        v1.MapAssetManagementEndpoints();
        v1.MapQrEndpoints();

        app.MapHub<UpdatesHub>("/hubs/updates", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization(AuthorizationPolicies.ViewDevices);
        return app;
    }
}
