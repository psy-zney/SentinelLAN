using System.Net.Mail;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public static class ManagementValidation
{
    public static bool IsValid(CreateUserRequest request) =>
        IsReasonConfirmed(request.Reason, request.Confirmed) &&
        IsEmail(request.Email) && !string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Trim().Length is >= 2 and <= 100 &&
        (string.IsNullOrEmpty(request.Password) || (request.Password.Length is >= 12 and <= 1024)) &&
        request.Role is Roles.Admin or Roles.Technician or Roles.Employee;

    public static bool IsValid(EnrollmentTokenRequest request) =>
        IsReasonConfirmed(request.Reason, request.Confirmed) && request.ValidForMinutes is >= 1 and <= 60;

    public static bool IsValid(DeviceAssignmentRequest request) => IsReasonConfirmed(request.Reason, request.Confirmed);
    public static bool IsValid(RevokeDeviceRequest request) => IsReasonConfirmed(request.Reason, request.Confirmed);

    private static bool IsReasonConfirmed(string? reason, bool confirmed) => confirmed && reason?.Trim().Length is >= 3 and <= 1000;

    private static bool IsEmail(string? value)
    {
        if (value is null || value.Trim().Length is < 3 or > 254) return false;
        try { return new MailAddress(value.Trim()).Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }
}
