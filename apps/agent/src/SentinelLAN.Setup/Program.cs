namespace SentinelLAN.Setup;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--check-configuration")
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string? Metadata(string key) => assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .Cast<System.Reflection.AssemblyMetadataAttribute>().SingleOrDefault(attribute => attribute.Key == key)?.Value;
            var server = SetupInstaller.CompanyServerUrl;
            using var package = assembly.GetManifestResourceStream("SentinelLAN.Agent.msi");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                ServerUrl = server,
                CompanyPinned = !string.IsNullOrWhiteSpace(server),
                CommandKeyId = Metadata("CommandKeyId"),
                HasCommandPublicKey = !string.IsNullOrWhiteSpace(Metadata("CommandPublicKey")),
                CommandPublicKeySha256 = Metadata("CommandPublicKey") is { Length: > 0 } publicKey
                    ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Convert.FromBase64String(publicKey))).ToLowerInvariant() : null,
                SourceRevision = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().SingleOrDefault()?.InformationalVersion.Split('+').ElementAtOrDefault(1),
                MsiPayloadIncluded = assembly.GetManifestResourceNames().Contains("SentinelLAN.Agent.msi", StringComparer.Ordinal),
                MsiSha256 = package is null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(package)),
                Version = assembly.GetName().Version?.ToString()
            }));
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm());
    }
}
