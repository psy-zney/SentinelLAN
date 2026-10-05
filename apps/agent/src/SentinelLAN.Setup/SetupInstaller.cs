using System.Diagnostics;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Enrollment;

namespace SentinelLAN.Setup;

internal static class SetupInstaller
{
    public static string? CompanyServerUrl => Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .SingleOrDefault(attribute => attribute.Key == "CompanyServerUrl")?.Value;
    private static string? Metadata(string key) => Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(attribute => attribute.Key == key)?.Value;
    private static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SentinelLAN", "Agent");
    public static bool IsEnrolled => File.Exists(Path.Combine(DataDirectory, "identity.dat"));

    public static async Task ConnectAsync(EnrollmentConnectionCode code, IProgress<string> progress)
    {
        if (IsEnrolled) throw new InvalidOperationException("Máy đã được đăng ký. Không cần dùng mã mới; liên hệ IT nếu cần đăng ký lại.");
        if (!string.IsNullOrWhiteSpace(CompanyServerUrl))
        {
            var keyId = Metadata("CommandKeyId");
            try
            {
                if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 64 ||
                    keyId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new FormatException();
                var publicKey = Convert.FromBase64String(Metadata("CommandPublicKey") ?? "");
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(publicKey, out var read);
                if (read != publicKey.Length || rsa.KeySize < 3072) throw new FormatException();
            }
            catch (Exception error) when (error is FormatException or CryptographicException)
            {
                throw new InvalidOperationException("Bộ cài công ty thiếu khóa xác minh hợp lệ. Nhờ IT cung cấp lại bộ cài.");
            }
        }
        var server = EnrollmentConnectionCode.NormalizeServerUrl(code.ServerUrl);
        progress.Report("Đang kiểm tra kết nối tới hệ thống công ty…");
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
        using var health = await client.GetAsync(server + "/health/ready");
        if (!health.IsSuccessStatusCode)
            throw new InvalidOperationException("Máy chủ chưa sẵn sàng. Liên hệ IT; mã chưa được sử dụng ở bước kiểm tra này.");

        var assembly = Assembly.GetExecutingAssembly();
        var stage = Path.Combine(Path.GetTempPath(), "SentinelLAN-Setup-" + Guid.NewGuid().ToString("N"));
        var directory = Directory.CreateDirectory(stage);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[] { WellKnownSidType.BuiltinAdministratorsSid, WellKnownSidType.LocalSystemSid })
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(security);
        var msi = Path.Combine(stage, "SentinelLAN.Agent.msi");
        var script = Path.Combine(stage, "configure-windows-agent.ps1");
        try
        {
            // The MSI contains the configurator; the downloadable EXE also embeds that MSI.
            await using var package = assembly.GetManifestResourceStream("SentinelLAN.Agent.msi");
            if (package is not null)
            {
                progress.Report("Đang cài đặt dịch vụ SentinelLAN…");
                await using (var file = File.Create(msi)) await package.CopyToAsync(file);
                var result = await RunAsync(Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                    ["/i", msi, "/qn", "/norestart"], null);
                if (result.ExitCode is not (0 or 3010))
                    throw new InvalidOperationException("Không thể cài đặt dịch vụ. Nhờ IT kiểm tra quyền cài đặt hoặc bộ cài đang chạy khác.");
            }
            await using (var resource = assembly.GetManifestResourceStream("configure-windows-agent.ps1")
                ?? throw new InvalidOperationException("Bộ cài thiếu thành phần cấu hình. Hãy tải lại bộ cài do IT chỉ định."))
            await using (var file = File.Create(script)) await resource.CopyToAsync(file);
            progress.Report("Đang đăng ký máy và chờ trạng thái đầu tiên…");
            // Execute our embedded configuration logic without changing the machine's script policy.
            // Only this fixed loader is in the command line; parameters and token travel over stdin.
            const string loader = "$ErrorActionPreference = 'Stop'; $setupInput = [Console]::ReadLine() | ConvertFrom-Json; & ([scriptblock]::Create([IO.File]::ReadAllText($setupInput.scriptPath))) -ServerUrl $setupInput.serverUrl -CommandPublicKey $setupInput.commandPublicKey -CommandKeyId $setupInput.commandKeyId -ReadTokenFromStandardInput";
            var configuration = await RunAsync(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(loader))],
                JsonSerializer.Serialize(new { scriptPath = script, serverUrl = server,
                    commandPublicKey = Metadata("CommandPublicKey"), commandKeyId = Metadata("CommandKeyId") }) + "\n" + code.Token);
            if (configuration.ExitCode != 0 || !configuration.Output.Contains("SETUP_STATE:Connected", StringComparison.Ordinal))
            {
                var message = configuration.Output.Contains("SETUP_STATE:TokenUsed", StringComparison.Ordinal)
                    ? "Mã kết nối đã được sử dụng. Vui lòng liên hệ IT để cấp mã mới."
                    : configuration.Output.Contains("SETUP_STATE:TokenExpired", StringComparison.Ordinal)
                        ? "Mã kết nối đã hết hạn. Vui lòng liên hệ IT để cấp mã mới."
                        : "Chưa xác nhận được kết nối. Nhờ IT kiểm tra mạng, mã và trạng thái Agent trước khi thử lại.";
                throw new InvalidOperationException(message);
            }
        }
        finally
        {
            // Delete only known extracted files; never recursively remove a computed directory.
            if (File.Exists(msi)) File.Delete(msi);
            if (File.Exists(script)) File.Delete(script);
            Directory.Delete(stage);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string executable, string[] arguments, string? standardInput)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // Let Windows PowerShell select compatible modules instead of inheriting PowerShell 7 paths.
        start.Environment.Remove("PSModulePath");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Không thể mở thành phần cài đặt.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (standardInput is not null) await process.StandardInput.WriteLineAsync(standardInput);
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        await error; // Drain stderr but never display raw scripts, secrets or remote response text.
        return (process.ExitCode, await output);
    }
}
