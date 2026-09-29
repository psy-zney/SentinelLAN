using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record LoginRequest(string OrganizationCode, string Email, string Password);

public record AuthSessionResponse(int ExpiresIn, string Role, string DisplayName);

public record CurrentSessionResponse(string Role, string DisplayName);
