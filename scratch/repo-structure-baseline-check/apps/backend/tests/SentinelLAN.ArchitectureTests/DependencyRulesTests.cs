namespace SentinelLAN.ArchitectureTests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void DomainProjectHasNoProjectReferences()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "apps", "backend", "src", "SentinelLAN.Domain", "SentinelLAN.Domain.csproj"));
        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SentinelLAN.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }
}
