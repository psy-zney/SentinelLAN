from pathlib import Path
from collections import defaultdict
import re

root = Path(__file__).resolve().parent.parent
api = root / 'apps/backend/src/SentinelLAN.Api'
application = root / 'apps/backend/src/SentinelLAN.Application'
baseline = root / 'scratch/repo-structure-baseline'


def write(path, source):
    path.resolve().relative_to(root)
    assert not path.exists(), f'Destination already exists: {path}'
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(source.rstrip() + '\n', encoding='utf-8', newline='\n')


def indent(source, spaces=8):
    return '\n'.join(' ' * spaces + line if line else '' for line in source.strip().splitlines())


def wrap_class(usings, namespace, name, members):
    return f'{usings.strip()}\n\nnamespace {namespace};\n\npublic static class {name}\n{{\n{members}\n}}\n'


program_path = api / 'Program.cs'
program = program_path.read_text(encoding='utf-8-sig')
assert program == (baseline / program_path.relative_to(root)).read_text(encoding='utf-8-sig')

# Move service registration and production-secret checks without changing their rules.
services = program[program.index('builder.Services.AddProblemDetails();'):program.index('var app = builder.Build();')]
services = services.replace('builder.Services', 'services').replace('builder.Configuration', 'configuration').replace('builder.Environment', 'environment')
secret_check = program[program.index('static bool IsUnsafeProductionSecret'):program.index('app.Run();')].strip()
secret_check = secret_check.replace('static bool', 'private static bool', 1)
service_usings = '''using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Infrastructure;'''
write(api / 'Extensions/ServiceCollectionExtensions.cs', wrap_class(
    service_usings, 'SentinelLAN.Api', 'ServiceCollectionExtensions',
    '    public static IServiceCollection AddSentinelLan(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)\n'
    '    {\n' + indent(services) + '\n        return services;\n    }\n\n' + indent(secret_check, 4)))

# Keep middleware order and initialization order exactly as before.
middleware = program[program.index('app.UseExceptionHandler();'):program.index('using (var scope = app.Services.CreateScope())')]
initialization = program[program.index('using (var scope = app.Services.CreateScope())'):program.index('app.MapGet("/",')]
initialization = initialization.replace('builder.Configuration', 'app.Configuration')
write(api / 'Extensions/WebApplicationExtensions.cs', wrap_class(
    'using Microsoft.EntityFrameworkCore;\nusing SentinelLAN.Application;\nusing SentinelLAN.Infrastructure;',
    'SentinelLAN.Api', 'WebApplicationExtensions',
    '    public static WebApplication UseSentinelLan(this WebApplication app)\n    {\n'
    + indent(middleware) + '\n        return app;\n    }\n\n'
    + '    public static async Task InitializeSentinelLanAsync(this WebApplication app)\n    {\n'
    + indent(initialization) + '\n    }'))

# Each registration is a complete top-level statement, including all metadata.
routes = program[program.index('v1.MapPost("/auth/login"'):program.index('app.MapHub<UpdatesHub>')]
routes = routes.replace('var mobile = v1.MapGroup("/mobile");\n', '').replace('var mobileAuth = mobile.MapGroup("/auth");\n', '')
routes = re.sub(r'^//[^\n]*\n', '', routes, flags=re.M)
matches = list(re.finditer(r'^(v1|mobileAuth|mobile)\.Map(Get|Post|Put|Delete)\("([^"\n]+)"', routes, re.M))
groups = defaultdict(list)
for i, match in enumerate(matches):
    statement = routes[match.start():matches[i+1].start() if i+1 < len(matches) else len(routes)].strip()
    receiver, verb, path = match.groups()
    if receiver in {'mobile', 'mobileAuth'}:
        group = 'MobileAuth'
    elif path.startswith('/auth/'):
        group = 'Auth'
    elif path.startswith('/users') or path == '/organizations':
        group = 'User'
    elif path == '/enrollment-tokens':
        group = 'Enrollment'
    elif path.startswith('/my-device'):
        group = 'MyDevice'
    elif path.startswith('/agent/'):
        group = 'Agent'
    elif path.startswith('/commands'):
        group = 'Command'
    elif path.startswith('/policies'):
        group = 'Policy'
    elif path.startswith('/alerts'):
        group = 'Alert'
    elif path == '/audit-logs':
        group = 'Audit'
    elif '/qr-label' in path or path.startswith('/qr/'):
        group = 'Qr'
    elif any(part in path for part in ['/asset-detail', '/asset-profile', '/timeline', '/incidents', '/work-orders', '/asset-loans']):
        group = 'AssetManagement'
    elif path.startswith('/devices') or path == '/dashboard':
        group = 'Device'
    else:
        raise ValueError(f'Unclassified route: {verb} {path}')
    groups[group].append(statement)

endpoint_usings = '''using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;'''
publish = program[program.index('static async Task<IResult> PublishDeviceUpdateAsync'):program.index('public partial class Program;')].strip()
publish = publish.replace('static async', 'private static async', 1)
for group, statements in groups.items():
    name = f'{group}Endpoints'
    body = '\n\n'.join(statements)
    if group == 'MobileAuth':
        body = 'var mobile = v1.MapGroup("/mobile");\nvar mobileAuth = mobile.MapGroup("/auth");\n\n' + body
    members = f'    public static void Map{name}(this RouteGroupBuilder v1)\n    {{\n{indent(body)}\n    }}'
    if group == 'Device':
        members += '\n\n' + indent(publish, 4)
    usings = endpoint_usings
    if group == 'Agent':
        usings = 'using System.Security.Cryptography;\n' + usings
    write(api / f'Endpoints/{name}.cs', wrap_class(usings, 'SentinelLAN.Api.Endpoints', name, members))

health = program[program.index('app.MapGet("/",'):program.index('var v1 = app.MapGroup("/api/v1");')]
write(api / 'Endpoints/HealthEndpoints.cs', wrap_class(
    'using Microsoft.EntityFrameworkCore;\nusing SentinelLAN.Infrastructure;',
    'SentinelLAN.Api.Endpoints', 'HealthEndpoints',
    '    public static void MapHealthEndpoints(this WebApplication app)\n    {\n' + indent(health) + '\n    }'))
mapping = 'app.MapHealthEndpoints();\n\nvar v1 = app.MapGroup("/api/v1");\nv1.MapPlatform();\nv1.MapSelfService();\n'
mapping += '\n'.join(f'v1.Map{group}Endpoints();' for group in groups)
mapping += '\n\napp.MapHub<UpdatesHub>("/hubs/updates").RequireAuthorization(AuthorizationPolicies.ViewDevices);'
write(api / 'Extensions/EndpointRouteBuilderExtensions.cs', wrap_class(
    'using SentinelLAN.Api.Endpoints;\nusing SentinelLAN.Application;',
    'SentinelLAN.Api', 'EndpointRouteBuilderExtensions',
    '    public static WebApplication MapSentinelLanEndpoints(this WebApplication app)\n    {\n'
    + indent(mapping) + '\n        return app;\n    }'))

provider = program[program.index('public sealed class HttpCurrentTenantProvider'):].strip()
write(api / 'HttpCurrentTenantProvider.cs', 'using SentinelLAN.Application;\n\nnamespace SentinelLAN.Api;\n\n' + provider)
program_path.write_text('''using SentinelLAN.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSentinelLan(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseSentinelLan();
await app.InitializeSentinelLanAsync();
app.MapSentinelLanEndpoints();
app.Run();

public partial class Program;
''', encoding='utf-8', newline='\n')

# Place cohesive Application modules in folders; namespaces stay compatible.
folders = {
    'Alerts': ['AlertService.cs'],
    'AssetManagement': ['AssetContracts.cs', 'AssetManagementService.cs', 'IncidentIdempotency.cs'],
    'Authentication': ['AuthenticationContracts.cs', 'AuthenticationService.cs'],
    'Devices': ['DeviceRequestValidation.cs', 'MyDeviceService.cs', 'QrManagementService.cs'],
    'Policies': ['PolicyService.cs'],
    'SelfService': ['SelfServiceCommunication.cs', 'SelfServiceContracts.cs', 'SelfServiceReconciliation.cs', 'SelfServiceService.cs'],
    'VpsNodes': ['VpsContracts.cs', 'VpsNodeService.cs'],
}
for folder, files in folders.items():
    (application / folder).mkdir(exist_ok=True)
    for file in files:
        src = application / file
        dst = application / folder / file
        src.resolve().relative_to(root)
        dst.resolve().relative_to(root)
        assert not dst.exists()
        src.rename(dst)


def declarations(source):
    matches = list(re.finditer(r'^(?:public|internal) (?:(?:sealed|static|readonly|abstract) )*(?:class|interface|enum|record(?: struct)?) (\w+)', source, re.M))
    return {match.group(1): source[match.start():matches[i+1].start() if i+1 < len(matches) else len(source)].strip()
            for i, match in enumerate(matches)}


management_folders = {
    'IManagementStore': 'Common', 'ManagementValidation': 'Common',
    'ISecretHasher': 'Common', 'DefaultSecretHasher': 'Common',
    'IEnrollmentSecretGenerator': 'Enrollment', 'EnrollmentTokenService': 'Enrollment',
    'IActivationTokenGenerator': 'Users', 'DefaultActivationTokenGenerator': 'Users',
    'CreateUserOutcome': 'Users', 'UserManagementService': 'Users',
    'IQrCodeGenerator': 'Devices', 'DefaultQrCodeGenerator': 'Devices',
    'DeviceManagementService': 'Devices', 'DeviceManagementOutcome': 'Devices', 'RevokeDeviceRequest': 'Devices',
}
management_path = application / 'ManagementServices.cs'
management = declarations(management_path.read_text(encoding='utf-8-sig'))
assert management.keys() == management_folders.keys()
for name, declaration in management.items():
    usings = 'using SentinelLAN.Domain;'
    if name == 'ManagementValidation':
        usings = 'using System.Net.Mail;\n' + usings
    write(application / management_folders[name] / f'{name}.cs', usings + '\n\nnamespace SentinelLAN.Application;\n\n' + declaration)
management_path.unlink()

contract_groups = {
    'Common/Permissions.cs': ['Permissions'],
    'Common/Roles.cs': ['Roles'],
    'Common/AuthorizationPolicies.cs': ['AuthorizationPolicies'],
    'Common/ActorContext.cs': ['ActorContext'],
    'Common/AgentContext.cs': ['AgentContext'],
    'Common/ICurrentTenantProvider.cs': ['ICurrentTenantProvider'],
    'Common/ManagementResultStatus.cs': ['ManagementResultStatus'],
    'Authentication/SessionContracts.cs': ['LoginRequest', 'AuthSessionResponse', 'CurrentSessionResponse'],
    'Devices/DeviceScope.cs': ['DeviceScope'],
    'Devices/DeviceContracts.cs': ['DeviceDto', 'DashboardDto', 'TelemetrySnapshotDto', 'DeviceAssignmentRequest'],
    'Devices/MyDeviceContracts.cs': ['EmployeeDeviceActionDto', 'EmployeeDeviceDto', 'PrivacyManifestDto', 'MyDeviceDto', 'ReportMyDeviceIncidentRequest'],
    'Devices/QrContracts.cs': ['GenerateQrLabelRequest', 'RevokeQrLabelRequest', 'QrLabelResponse', 'QrLabelStatusDto', 'PublicQrResolveDto', 'AuthenticatedQrResolveDto'],
    'Enrollment/EnrollmentContracts.cs': ['EnrollRequest', 'EnrollResponse', 'HeartbeatRequest', 'EnrollmentTokenRequest', 'EnrollmentTokenResponse'],
    'Commands/CommandContracts.cs': ['CreateCommandRequest', 'CommandResultRequest', 'CommandDto'],
    'Commands/ICommandSigner.cs': ['ICommandSigner'],
    'Policies/PolicyContracts.cs': ['PolicyDto', 'AgentPolicyDto', 'CreatePolicyRequest', 'UpdatePolicyRequest', 'AssignPolicyRequest'],
    'Alerts/AlertContracts.cs': ['AlertDto', 'CreateAlertRequest'],
    'Audit/AuditLogDto.cs': ['AuditLogDto'],
    'Users/UserContracts.cs': ['UserSummaryDto', 'CreateUserRequest', 'CreateUserResponse', 'SetUserStatusRequest', 'ReissueActivationTokenRequest', 'ReissueActivationTokenResponse', 'RevokeActivationTokenRequest', 'ActivateAccountRequest', 'ValidateActivationTokenRequest', 'ValidateActivationTokenResponse'],
    'Organizations/OrganizationSummaryDto.cs': ['OrganizationSummaryDto'],
}
contracts_path = application / 'Contracts.cs'
contracts = declarations(contracts_path.read_text(encoding='utf-8-sig'))
names = [name for group in contract_groups.values() for name in group]
assert len(names) == len(set(names)) and set(names) == contracts.keys()
for file, names in contract_groups.items():
    write(application / file, 'using SentinelLAN.Domain;\n\nnamespace SentinelLAN.Application;\n\n' + '\n\n'.join(contracts[name] for name in names))
contracts_path.unlink()

# Existing modular APIs join Endpoints; SelfService registration joins Extensions.
self_service_path = api / 'SelfServiceApi.cs'
self_service = self_service_path.read_text(encoding='utf-8-sig')
registration_start = self_service.index('    public static IServiceCollection AddSelfService')
registration_end = self_service.index('    public static RouteGroupBuilder MapSelfService')
registration = self_service[registration_start:registration_end].rstrip()
write(api / 'Extensions/SelfServiceServiceCollectionExtensions.cs', wrap_class(
    'using SentinelLAN.Application;\nusing SentinelLAN.Infrastructure;',
    'SentinelLAN.Api', 'SelfServiceServiceCollectionExtensions', registration))
write(api / 'Endpoints/SelfServiceApi.cs', self_service[:registration_start] + self_service[registration_end:])
self_service_path.unlink()
platform_path = api / 'Platform/PlatformApi.cs'
platform_dst = api / 'Endpoints/PlatformApi.cs'
assert not platform_dst.exists()
platform_path.resolve().relative_to(root)
platform_dst.resolve().relative_to(root)
platform_path.rename(platform_dst)

print(f'Extracted {len(matches)} route registrations into {len(groups)} domain endpoint classes.')
print(f'Program.cs: {len(program.splitlines())} -> 12 lines.')
print(f'Split {len(management)} management types and {len(contracts)} contracts by owning domain.')
