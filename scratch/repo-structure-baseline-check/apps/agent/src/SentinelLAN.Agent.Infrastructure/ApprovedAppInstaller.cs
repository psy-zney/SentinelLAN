using System.Security.Cryptography;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public interface IWindowsMsiOperations
{
    bool CanInstall { get; }
    Task<bool> VerifyPublisherAsync(string packagePath, string publisherThumbprint, CancellationToken cancellationToken);
    Task<ExecutionResult> InstallAsync(string packagePath, CancellationToken cancellationToken);
    Guid? RegisteredSentinelProductCode();
    Task<ExecutionResult> UninstallAsync(Guid productCode, CancellationToken cancellationToken);
}

/// <summary>Only downloads and installs a signed, immutable catalog MSI approved for this device.</summary>
public sealed class ApprovedAppInstaller(AgentExecutionOptions options, string packageDirectory,
    IWindowsMsiOperations platform, HttpClient? downloadClient = null, TimeProvider? clock = null)
{
    private static readonly byte[] CompoundFileHeader = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<ExecutionResult> InstallAsync(RemoteCommand command, CancellationToken cancellationToken)
    {
        if (!options.AllowApprovedAppInstall) return new(false, "Approved app installation is disabled; IT must explicitly configure SENTINELLAN_ALLOW_APPROVED_APP_INSTALL on this device");
        if (!platform.CanInstall) return new(false, "Approved MSI installation requires an explicitly authorized elevated Windows installer deployment; no local administrator membership was granted");
        if (command.Type != "InstallApprovedApp" || command.ExpiresAt <= _clock.GetUtcNow() || !SelfServiceCommandParameters.TryReadApp(command, _clock.GetUtcNow(), out var parameters))
            return new(false, "Approved catalog package or approval expiry is invalid");
        var package = new Uri(parameters!.PackageUrl);
        if (options.TrustedPackageHosts is not { Length: > 0 } hosts ||
            !hosts.Contains(package.IdnHost, StringComparer.OrdinalIgnoreCase))
            return new(false, "Package host is not in this device's trusted HTTPS package host allowlist");
        if (options.MaxPackageBytes is < 1024 or > 100 * 1024 * 1024)
            return new(false, "Package size limit must be between 1 KiB and 100 MiB");
        var remaining = (command.ExpiresAt < parameters.ApprovalExpiresAt ? command.ExpiresAt : parameters.ApprovalExpiresAt) - _clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return new(false, "Installation approval expired");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(remaining);
        ProtectedStoreDirectory.EnsurePrivate(packageDirectory);
        var packagePath = Path.Combine(packageDirectory, $"approved-{command.Id:N}-{Guid.NewGuid():N}.msi");
        using var ownedClient = downloadClient is null
            ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan }
            : null;
        try
        {
            using var response = await (downloadClient ?? ownedClient!).GetAsync(package, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            // No redirects are accepted, including redirects to another otherwise trusted host.
            if ((int)response.StatusCode is < 200 or >= 300) return new(false, "Package download failed or attempted a redirect");
            if (response.RequestMessage?.RequestUri is { } finalUri && finalUri != package)
                return new(false, "Package download changed its approved URL");
            if (response.Content.Headers.ContentLength is { } length && (length <= 0 || length > options.MaxPackageBytes))
                return new(false, "Package exceeds the configured download limit");
            await using var source = await response.Content.ReadAsStreamAsync(deadline.Token);
            var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long count = 0;
            var buffer = new byte[64 * 1024];
            await using (var destination = new FileStream(packagePath, fileOptions))
            {
                int read;
                while ((read = await source.ReadAsync(buffer, deadline.Token)) > 0)
                {
                    count += read;
                    if (count > options.MaxPackageBytes) return new(false, "Package exceeds the configured download limit");
                    hash.AppendData(buffer.AsSpan(0, read));
                    await destination.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
                }
                await destination.FlushAsync(deadline.Token);
            }
            if (!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(parameters.Sha256)))
                return new(false, "Package SHA256 does not match the immutable IT approval");
            await using (var packageFile = File.OpenRead(packagePath))
            {
                var header = new byte[8];
                if (await packageFile.ReadAsync(header, deadline.Token) != 8 || !header.AsSpan().SequenceEqual(CompoundFileHeader))
                    return new(false, "Approved package is not an MSI compound document");
            }
            if (!await platform.VerifyPublisherAsync(packagePath, parameters.PublisherThumbprint, deadline.Token))
                return new(false, "Package Authenticode trust or approved publisher thumbprint could not be verified");
            if (_clock.GetUtcNow() >= parameters.ApprovalExpiresAt || _clock.GetUtcNow() >= command.ExpiresAt)
                return new(false, "Approval expired before installation; no installer started");
            return await platform.InstallAsync(packagePath, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Approval window expired; installation completion was not confirmed. IT must check Windows Installer status");
        }
        finally { if (File.Exists(packagePath)) File.Delete(packagePath); }
    }
}
