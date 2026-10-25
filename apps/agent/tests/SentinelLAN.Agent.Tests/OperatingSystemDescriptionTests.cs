using System.Runtime.InteropServices;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class OperatingSystemDescriptionTests
{
    [Theory]
    [InlineData("Windows 10 Pro", "Client", 22000, "Windows 11 Pro")]
    [InlineData("Windows 10 Home", "Client", 26300, "Windows 11 Home")]
    [InlineData("Windows 10 Enterprise", "Client", 19045, "Windows 10 Enterprise")]
    [InlineData("Windows 10 Pro", "Client", 21999, "Windows 10 Pro")]
    [InlineData("Windows 11 Pro", "Client", 26100, "Windows 11 Pro")]
    [InlineData("Windows Server 2025 Standard", "Server", 26100, "Windows Server 2025 Standard")]
    [InlineData("Windows Server 2025 Datacenter", "Server Core", 26100, "Windows Server 2025 Datacenter")]
    [InlineData(null, "Client", 26300, "Windows 11")]
    [InlineData(null, null, 26300, "Windows")]
    [InlineData("Windows 10 Pro", null, 26300, "Windows 10 Pro")]
    public void WindowsNameUsesClientBuildWithoutRelabelingServers(string? productName, string? installationType,
        int build, string expectedName)
    {
        var result = OperatingSystemDescription.FormatWindows(productName, installationType, new Version(10, 0, build, 9711), "26H2");
        Assert.Equal($"{expectedName} 26H2 (build 10.0.{build}.9711)", result);
    }

    [Fact]
    public void MissingRegistryMetadataKeepsTheTechnicalVersion()
    {
        Assert.Equal("Windows (build 10.0.26300.0)",
            OperatingSystemDescription.FormatWindows(null, null, new Version(10, 0, 26300, 0), null));
    }

    [Fact]
    public void NonWindowsDescriptionIsPreserved()
    {
        if (OperatingSystem.IsWindows()) return;
        Assert.Equal(RuntimeInformation.OSDescription, OperatingSystemDescription.GetCurrent());
    }
}
