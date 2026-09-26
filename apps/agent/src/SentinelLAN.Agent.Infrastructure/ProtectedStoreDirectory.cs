using System.Security.AccessControl;
using System.Security.Principal;

namespace SentinelLAN.Agent.Infrastructure;

internal static class ProtectedStoreDirectory
{
    private const UnixFileMode PrivateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode SharedMode = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    public static void Ensure(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        // Create new directories with private permissions without changing a shared parent such as /tmp.
        Directory.CreateDirectory(directory, PrivateMode);
        if ((File.GetUnixFileMode(directory) & SharedMode) != 0)
            throw new UnauthorizedAccessException("The Agent data directory must be accessible only to its owner.");
    }

    public static void EnsurePrivate(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            var info = new DirectoryInfo(directory);
            var current = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException("Agent process identity is unavailable.");
            var trusted = new HashSet<string>(StringComparer.Ordinal) { "S-1-5-18", "S-1-5-19", "S-1-5-32-544", current.Value };
            if (!info.Exists)
            {
                var security = new DirectorySecurity();
                security.SetAccessRuleProtection(true, false);
                foreach (var sid in trusted)
                    security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                info.Create(security);
                info.Refresh();
            }
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Agent protected data directory cannot be a reparse point.");
            var writable = FileSystemRights.Write | FileSystemRights.Modify | FileSystemRights.Delete |
                FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
            foreach (FileSystemAccessRule rule in info.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)))
                if (rule.AccessControlType == AccessControlType.Allow && (rule.FileSystemRights & writable) != 0 &&
                    !trusted.Contains(((SecurityIdentifier)rule.IdentityReference).Value))
                    throw new UnauthorizedAccessException("Agent data directory is writable by an untrusted Windows principal.");
            return;
        }

        Ensure(directory);
    }
}
