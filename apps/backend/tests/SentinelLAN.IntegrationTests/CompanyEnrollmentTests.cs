using SentinelLAN.Enrollment;

namespace SentinelLAN.IntegrationTests;

public sealed class CompanyEnrollmentTests
{
    [Fact]
    public void OpaqueTokenUsesOnlyCompanyConfiguredDestination()
    {
        var resolved = CompanyEnrollment.Resolve("  " + new string('A', 64) + "  ", "https://company.test/");
        Assert.Equal("https://company.test", resolved.ServerUrl);
        Assert.Equal(new string('A', 64), resolved.Token);
    }

    [Theory]
    [InlineData("SL1.e30")]
    [InlineData("https://attacker.test")]
    [InlineData("")]
    [InlineData("AAAAAAAA")]
    public void CompanyInstallerRejectsPayloadsThatCouldChooseAnotherServer(string token) =>
        Assert.Throws<FormatException>(() => CompanyEnrollment.Resolve(token, "https://company.test"));

    [Fact]
    public void InsecureLanServerIsRejected() =>
        Assert.Throws<FormatException>(() => CompanyEnrollment.Resolve(new string('A', 64), "http://192.168.1.10"));
}
