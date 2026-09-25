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
}
