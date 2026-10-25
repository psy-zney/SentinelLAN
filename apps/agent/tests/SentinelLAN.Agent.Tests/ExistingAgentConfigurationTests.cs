using SentinelLAN.Setup;

namespace SentinelLAN.Agent.Tests;

public sealed class ExistingAgentConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("https://company.test")]
    [InlineData("https://company.test:443/")]
    public void UsesTheRegisteredServerWithoutRequiringAnotherCode(string? companyServer)
    {
        Assert.Equal("https://company.test", ExistingAgentConfiguration.ResolveServer(
            """{"SENTINELLAN_API_URL":"https://company.test/","unrelated":"preserved"}""", companyServer));
    }

    [Fact]
    public void RejectsAnotherCompanyInsteadOfRepointingAnExistingIdentity()
    {
        Assert.Throws<InvalidOperationException>(() => ExistingAgentConfiguration.ResolveServer(
            """{"SENTINELLAN_API_URL":"https://other-company.test"}""", "https://company.test"));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"SENTINELLAN_API_URL\":42}")]
    [InlineData("{\"SENTINELLAN_API_URL\":\"http://company.test\"}")]
    [InlineData("{\"SENTINELLAN_API_URL\":\"https://company.test/api\"}")]
    public void InvalidSettingsDoNotExposePrivateConfiguration(string contents)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ExistingAgentConfiguration.ResolveServer(contents, null));
        Assert.Contains("Cấu hình máy đã đăng ký chưa hợp lệ", exception.Message);
        Assert.DoesNotContain(contents, exception.Message);
    }
}
