using System.Text;
using Renci.SshNet;
using SentinelLAN.Application;

namespace SentinelLAN.Infrastructure;

public sealed class SshNetVpsSshService : IVpsSshService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public async Task<VpsConnectionTestResultDto> TestConnectionAsync(
        string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default)
    {
        var metrics = await CollectMetricsAsync(host, port, username, decryptedPrivateKey, hostKeyFingerprint, cancellationToken);
        return new(metrics.Success, metrics.Message, metrics.OsInfo, metrics.Uptime, metrics.CpuPercent, metrics.RamPercent, metrics.DiskPercent, metrics.DockerContainersCount);
    }

    public async Task<VpsMetricsResultDto> CollectMetricsAsync(
        string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateClient(host, port, username, decryptedPrivateKey, hostKeyFingerprint);
            await client.ConnectAsync(cancellationToken);
            using var cmd = client.CreateCommand(VpsCommandCatalog.Probe.Replace("\r\n", "\n", StringComparison.Ordinal));
            cmd.CommandTimeout = TimeSpan.FromSeconds(60);
            await cmd.ExecuteAsync(cancellationToken);
            if (cmd.ExitStatus != 0) return new(false, "Probe VPS thất bại. Kiểm tra Linux, systemd và quyền tài khoản SSH.");
            return VpsProbeParser.Parse(cmd.Result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new(false, $"Không kiểm tra được VPS qua SSH: {ex.Message}"); }
    }

    public Task<VpsCommandResultDto> RestartServiceAsync(
        string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, string serviceName, CancellationToken cancellationToken = default)
    {
        string command;
        try { command = VpsCommandCatalog.BuildRestart(serviceName); }
        catch (ArgumentException) { return Task.FromResult(new VpsCommandResultDto(false, "Dịch vụ không thuộc danh sách cho phép.")); }
        return ExecuteAsync(host, port, username, decryptedPrivateKey, hostKeyFingerprint, command, "Đã khởi động lại dịch vụ và xác minh trạng thái hoạt động.", cancellationToken);
    }

    public Task<VpsCommandResultDto> ExecuteOperationAsync(
        string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, VpsOperationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ExpiresAt <= DateTimeOffset.UtcNow) return Task.FromResult(new VpsCommandResultDto(false, "Yêu cầu đã hết hạn."));
        string command;
        try { command = VpsCommandCatalog.BuildOperation(request); }
        catch (ArgumentException) { return Task.FromResult(new VpsCommandResultDto(false, "Lệnh VPS không hợp lệ.")); }
        return ExecuteAsync(host, port, username, decryptedPrivateKey, hostKeyFingerprint, command,
            request.Action == "Reboot" ? "VPS đã nhận lịch khởi động lại sau khoảng 1 phút. Các dịch vụ sẽ tạm gián đoạn; kiểm tra lại để xác minh VPS trở lại." : "Đã áp dụng và xác minh cấu hình tự khởi động.", cancellationToken, request.ExpiresAt);
    }

    private static async Task<VpsCommandResultDto> ExecuteAsync(string host, int port, string username, string key, string fingerprint,
        string command, string message, CancellationToken cancellationToken, DateTimeOffset? expiresAt = null)
    {
        try
        {
            using var client = CreateClient(host, port, username, key, fingerprint);
            await client.ConnectAsync(cancellationToken);
            if (expiresAt <= DateTimeOffset.UtcNow) return new(false, "Yêu cầu hết hạn trong lúc kết nối SSH.");
            using var cmd = client.CreateCommand(command);
            cmd.CommandTimeout = TimeSpan.FromSeconds(25);
            await cmd.ExecuteAsync(cancellationToken);
            return cmd.ExitStatus == 0 ? new(true, message) : new(false, $"VPS từ chối lệnh hoặc chưa xác minh được kết quả (exit {cmd.ExitStatus}). Kiểm tra quyền sudo/Docker và trạng thái VPS.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return new(false, "Lỗi kết nối hoặc hết thời gian chờ SSH. Kiểm tra VPS trước khi gửi lệnh mới."); }
    }

    private static SshClient CreateClient(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint)
    {
        if (string.IsNullOrWhiteSpace(hostKeyFingerprint) || !hostKeyFingerprint.StartsWith("SHA256:", StringComparison.Ordinal))
            throw new InvalidOperationException("A verified SSH SHA256 host key fingerprint is required.");
        var keyBytes = Encoding.UTF8.GetBytes(decryptedPrivateKey);
        using var stream = new MemoryStream(keyBytes);
        var keyFile = new PrivateKeyFile(stream);

        var connectionInfo = new ConnectionInfo(
            host,
            port,
            username,
            new PrivateKeyAuthenticationMethod(username, keyFile)
        )
        {
            Timeout = DefaultTimeout
        };

        var client = new SshClient(connectionInfo);
        client.HostKeyReceived += (_, args) =>
        {
            args.CanTrust = string.Equals("SHA256:" + args.FingerPrintSHA256, hostKeyFingerprint, StringComparison.Ordinal);
        };
        return client;
    }

}
