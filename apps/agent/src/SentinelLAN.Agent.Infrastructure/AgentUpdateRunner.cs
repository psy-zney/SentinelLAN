using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Runtime.Versioning;

namespace SentinelLAN.Agent.Infrastructure;

public static class AgentUpdateRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var platform = new WindowsAgentUpdatePlatform();
        if (!OperatingSystem.IsWindows() || !platform.CanUpdate) throw new UnauthorizedAccessException("Run updates through the configured Windows SYSTEM task.");
        var parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SentinelLAN");
        var directory = Path.Combine(parent, "Updates");
        AssertAdminDirectory(parent);
        AssertAdminDirectory(directory);
        if (!File.Exists(Path.Combine(parent, "Agent", "identity.dat")))
            throw new InvalidOperationException("The Agent must already be enrolled; automatic updates never enroll a device.");
        var settingsPath = Path.Combine(directory, "update-settings.json");
        if ((File.GetAttributes(settingsPath) & FileAttributes.ReparsePoint) != 0 || new FileInfo(settingsPath).Length > 16 * 1024)
            throw new InvalidDataException("Updater settings are invalid.");
        var settings = JsonSerializer.Deserialize<AgentUpdateSettings>(await File.ReadAllTextAsync(settingsPath, cancellationToken),
            JsonOptions) ?? throw new InvalidDataException("Missing updater settings.");
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, CheckCertificateRevocationList = true })
        { Timeout = Timeout.InfiniteTimeSpan };
        var state = await new AgentAutoUpdater(http, platform, directory).RunAsync(settings, cancellationToken);
        Console.WriteLine(state);
        return state is "Failed" or "InstallFailed" or "AlreadyAttempted" ? 1 : state == "RestartRequired" ? 3010 : 0;
    }

    [SupportedOSPlatform("windows")]
    private static void AssertAdminDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new UnauthorizedAccessException("Updater directories must be real, administrator-protected directories.");
        var acl = info.GetAccessControl();
        var trusted = new[] { "S-1-5-18", "S-1-5-32-544" };
        if (!trusted.Contains(acl.GetOwner(typeof(SecurityIdentifier))?.Value, StringComparer.Ordinal))
            throw new UnauthorizedAccessException("Updater directory owner must be SYSTEM or Administrators.");
        var writable = FileSystemRights.Write | FileSystemRights.Modify | FileSystemRights.Delete |
            FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0 &&
                (rule.FileSystemRights & writable) != 0 && !trusted.Contains(((SecurityIdentifier)rule.IdentityReference).Value, StringComparer.Ordinal))
                throw new UnauthorizedAccessException("Only SYSTEM and Administrators may modify updater files.");
    }
}
