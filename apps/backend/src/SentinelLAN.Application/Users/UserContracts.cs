using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record UserSummaryDto(Guid Id, string Email, string DisplayName, string Role, string Status, DateTimeOffset CreatedAt, bool HasActiveInvitation = false);

public record CreateUserRequest
{
    [System.Text.Json.Serialization.JsonConstructor]
    public CreateUserRequest(string email, string displayName, string role, string reason, bool confirmed, string? password = null, int validForHours = 24)
    {
        Email = email;
        DisplayName = displayName;
        Role = role;
        Reason = reason;
        Confirmed = confirmed;
        Password = password;
        ValidForHours = validForHours;
    }

    public CreateUserRequest(string email, string displayName, string role, string password, string reason, bool confirmed)
        : this(email, displayName, role, reason, confirmed, password, 24) { }

    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool Confirmed { get; init; }
    public string? Password { get; init; }
    public int ValidForHours { get; init; } = 24;

    public void Deconstruct(out string email, out string displayName, out string role, out string reason, out bool confirmed, out string? password, out int validForHours)
    {
        email = Email;
        displayName = DisplayName;
        role = Role;
        reason = Reason;
        confirmed = Confirmed;
        password = Password;
        validForHours = ValidForHours;
    }
}

public record CreateUserResponse(UserSummaryDto User, string? ActivationToken, string? ActivationUrl, DateTimeOffset? ExpiresAt);

public record SetUserStatusRequest(string Status, string Reason, bool Confirmed);

public record ReissueActivationTokenRequest(string Reason, bool Confirmed, int ValidForHours = 24);

public record ReissueActivationTokenResponse(string ActivationToken, string ActivationUrl, DateTimeOffset ExpiresAt);

public record RevokeActivationTokenRequest(string Reason, bool Confirmed);

public record ActivateAccountRequest(string Token, string Password);

public record ValidateActivationTokenRequest(string? Token);

public record ValidateActivationTokenResponse(bool Valid, string? Message = null);
