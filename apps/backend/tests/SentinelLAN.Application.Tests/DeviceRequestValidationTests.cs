using SentinelLAN.Application;

namespace SentinelLAN.Application.Tests;

public sealed class DeviceRequestValidationTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidMetricsAreRejected(double value)
    {
        Assert.False(DeviceRequestValidation.IsValid(new HeartbeatRequest("key", value, 1, 1, "Windows", "test")));
        Assert.False(DeviceRequestValidation.IsValid(new HeartbeatRequest("key", 1, value, 1, "Windows", "test")));
        Assert.False(DeviceRequestValidation.IsValid(new HeartbeatRequest("key", 1, 1, value, "Windows", "test")));
    }

    [Fact]
    public void RequiresBoundedIdentifiersAndAcceptsPercentageBoundaries()
    {
        Assert.True(DeviceRequestValidation.IsValid(new HeartbeatRequest("key", 0, 100, 50, "Windows", "test")));
        Assert.False(DeviceRequestValidation.IsValid(new HeartbeatRequest(" ", 0, 100, 50, "Windows", "test")));
        Assert.False(DeviceRequestValidation.IsValid(new HeartbeatRequest(new string('k', 129), 0, 100, 50, "Windows", "test")));
        Assert.False(DeviceRequestValidation.IsValid(new EnrollRequest("token", " ", "Windows", "test")));
        Assert.False(DeviceRequestValidation.IsValid(new EnrollRequest("token", new string('x', 201), "Windows", "test")));
    }
}
