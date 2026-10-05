using SentinelLAN.Domain;

namespace SentinelLAN.Domain.Tests;

public sealed class SupportRequestTransitionTests
{
    [Theory]
    [InlineData("Incident", "Open", "Closed", true, false)]
    [InlineData("Incident", "Open", "Resolved", false, false)]
    [InlineData("Incident", "Open", "InProgress", false, true)]
    [InlineData("Incident", "InProgress", "Resolved", false, true)]
    [InlineData("Incident", "Resolved", "Closed", true, true)]
    [InlineData("Incident", "Closed", "Open", true, true)]
    [InlineData("Panic", "Closed", "Open", false, true)]
    [InlineData("Appointment", "AwaitingEmployee", "Closed", true, true)]
    [InlineData("InstallApp", "Open", "InProgress", false, false)]
    [InlineData("InstallApp", "Approved", "Open", false, false)]
    [InlineData("PauseAgent", "Approved", "Open", false, false)]
    [InlineData("UninstallAgent", "Approved", "Closed", true, false)]
    [InlineData("InstallApp", "Approved", "InProgress", false, true)]
    [InlineData("InstallApp", "Resolved", "Closed", true, true)]
    [InlineData("Privilege", "Closed", "Open", true, false)]
    [InlineData("PauseAgent", "Closed", "Open", false, false)]
    [InlineData("Incident", "Rejected", "Open", false, false)]
    [InlineData("Incident", "Resolved", "Approved", false, false)]
    public void LifecycleRejectsSkippedWorkAndRepeatedApproval(string kind, string current, string next, bool employee, bool allowed) =>
        Assert.Equal(allowed, SupportRequestTransitions.CanChange(kind, current, next, employee));
}
