using System.Xml.Linq;

namespace SentinelLAN.ArchitectureTests;

public sealed class DependencyRulesTests
{
    [Theory]
    [InlineData("apps/backend/src/SentinelLAN.Domain/SentinelLAN.Domain.csproj", "")]
    [InlineData("apps/backend/src/SentinelLAN.Application/SentinelLAN.Application.csproj", "SentinelLAN.Domain")]
    [InlineData("apps/backend/src/SentinelLAN.Infrastructure/SentinelLAN.Infrastructure.csproj", "SentinelLAN.Application|SentinelLAN.Domain")]
    [InlineData("apps/backend/src/SentinelLAN.Api/SentinelLAN.Api.csproj", "SentinelLAN.Application|SentinelLAN.Enrollment|SentinelLAN.Infrastructure")]
    [InlineData("apps/agent/src/SentinelLAN.Agent.Core/SentinelLAN.Agent.Core.csproj", "")]
    [InlineData("apps/agent/src/SentinelLAN.Agent.Infrastructure/SentinelLAN.Agent.Infrastructure.csproj", "SentinelLAN.Agent.Core")]
    [InlineData("apps/agent/src/SentinelLAN.Agent/SentinelLAN.Agent.csproj", "SentinelLAN.Agent.Core|SentinelLAN.Agent.Infrastructure")]
    [InlineData("apps/agent/src/SentinelLAN.Setup/SentinelLAN.Setup.csproj", "SentinelLAN.Enrollment")]
    [InlineData("packages/enrollment/SentinelLAN.Enrollment.csproj", "")]
    public void ProjectDependenciesRespectLayerBoundaries(string relativePath, string expectedReferences)
    {
        var project = XDocument.Load(Path.Combine(FindRepositoryRoot(), relativePath));
        var references = project.Descendants().Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedReferences.Split('|', StringSplitOptions.RemoveEmptyEntries), references);
    }

    [Theory]
    [InlineData("apps/backend/src/SentinelLAN.Domain/SentinelLAN.Domain.csproj")]
    [InlineData("apps/backend/src/SentinelLAN.Application/SentinelLAN.Application.csproj")]
    [InlineData("apps/agent/src/SentinelLAN.Agent.Core/SentinelLAN.Agent.Core.csproj")]
    public void BusinessLayersAndAgentCoreDoNotImportInfrastructurePackages(string relativePath)
    {
        var project = XDocument.Load(Path.Combine(FindRepositoryRoot(), relativePath));
        Assert.DoesNotContain(project.Descendants(), element => element.Name.LocalName == "PackageReference");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SentinelLAN.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }
}
