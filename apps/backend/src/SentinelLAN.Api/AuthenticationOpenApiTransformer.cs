using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SentinelLAN.Api;

public sealed class AuthenticationOpenApiTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    private const string AccessCookieScheme = "accessCookie";
    private const string RefreshCookieScheme = "refreshCookie";
    private const string CsrfScheme = "csrfHeader";
    private const string AgentIdScheme = "agentDeviceId";
    private const string AgentSecretScheme = "agentDeviceSecret";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[AccessCookieScheme] = ApiKey(ParameterLocation.Cookie, AuthCookieManager.AccessCookieName, "Short-lived HttpOnly browser access cookie.");
        document.Components.SecuritySchemes[RefreshCookieScheme] = ApiKey(ParameterLocation.Cookie, AuthCookieManager.RefreshCookieName, "Rotating HttpOnly browser refresh cookie.");
        document.Components.SecuritySchemes[CsrfScheme] = ApiKey(ParameterLocation.Header, AuthCookieManager.CsrfHeaderName, "Required with value 1 on state-changing cookie requests.");
        document.Components.SecuritySchemes[AgentIdScheme] = ApiKey(ParameterLocation.Header, AgentAuthenticationDefaults.DeviceIdHeader, "Enrolled device identifier.");
        document.Components.SecuritySchemes[AgentSecretScheme] = ApiKey(ParameterLocation.Header, AgentAuthenticationDefaults.DeviceSecretHeader, "Per-device secret; never place it in a URL or request body.");
        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var authorization = metadata.OfType<IAuthorizeData>().ToArray();
        var path = context.Description.RelativePath ?? string.Empty;
        var method = context.Description.HttpMethod ?? HttpMethods.Get;

        if (authorization.Any(item => item.Policy == SentinelLAN.Application.AuthorizationPolicies.Agent))
        {
            AddRequirement(operation, context.Document!, AgentIdScheme, AgentSecretScheme);
            return Task.CompletedTask;
        }

        if (path is "api/v1/auth/refresh" or "api/v1/auth/logout")
        {
            AddRequirement(operation, context.Document!, RefreshCookieScheme, CsrfScheme);
            return Task.CompletedTask;
        }

        if (authorization.Length > 0 && metadata.OfType<IAllowAnonymous>().Any() is false)
        {
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
                AddRequirement(operation, context.Document!, AccessCookieScheme);
            else
                AddRequirement(operation, context.Document!, AccessCookieScheme, CsrfScheme);
        }

        return Task.CompletedTask;
    }

    private static OpenApiSecurityScheme ApiKey(ParameterLocation location, string name, string description) => new()
    {
        Type = SecuritySchemeType.ApiKey,
        In = location,
        Name = name,
        Description = description
    };

    private static void AddRequirement(OpenApiOperation operation, OpenApiDocument document, params string[] schemeNames)
    {
        var requirement = new OpenApiSecurityRequirement();
        foreach (var schemeName in schemeNames)
            requirement[new OpenApiSecuritySchemeReference(schemeName, document)] = [];
        operation.Security ??= [];
        operation.Security.Add(requirement);
    }
}
