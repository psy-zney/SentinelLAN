using System.Security.Cryptography;
using System.Text.Json;

namespace SentinelLAN.Agent.Infrastructure;

/// <summary>Runs once from an administrator-protected task, independently of the Agent service.</summary>
public sealed class AgentAutoUpdater(HttpClient http, IAgentUpdatePlatform platform, string directory, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private static readonly byte[] MsiHeader = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];

    public async Task<string> RunAsync(AgentUpdateSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) return "Disabled";
        if (!platform.CanUpdate) throw new UnauthorizedAccessException("Updates require the installed Windows SYSTEM task.");
        AgentReleaseVerifier.ValidateSettings(settings);
        var installed = platform.InstalledVersion ?? throw new InvalidOperationException("No registered SentinelLAN MSI was found.");
        Directory.CreateDirectory(directory);
        await using var runLock = new FileStream(Path.Combine(directory, "run.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        AgentRelease? release = null;
        string? packagePath = null;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(5));
            using var manifestBuffer = new MemoryStream();
            await DownloadAsync(settings.ManifestUrl, manifestBuffer, 64 * 1024, null, deadline.Token);
            release = AgentReleaseVerifier.Verify(manifestBuffer.ToArray(), settings, _clock.GetUtcNow());
            _ = AgentReleaseVerifier.TryVersion(release.Version, out var target);
            if (target <= installed) return await RecordAsync("UpToDate", release);
            var attemptPath = Path.Combine(directory, $"attempt-{release.ReleaseId:N}.json");
            if (File.Exists(attemptPath)) return await RecordAsync("AlreadyAttempted", release);

            packagePath = Path.Combine(directory, $"package-{Guid.NewGuid():N}.msi");
            await using (var download = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await DownloadAsync(release.PackageUrl, download, release.SizeBytes, release.SizeBytes, deadline.Token);
            // Deny writes/deletion from hash verification until Windows Installer has completed.
            await using var package = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = new byte[8];
            await package.ReadExactlyAsync(header, deadline.Token);
            package.Position = 0;
            var hash = await SHA256.HashDataAsync(package, deadline.Token);
            if (!header.AsSpan().SequenceEqual(MsiHeader) || !CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(release.Sha256)))
                throw new InvalidDataException("Downloaded MSI hash or file header does not match the signed release.");
            if (!await platform.VerifyPackageAsync(packagePath, release, settings.PublisherThumbprint, deadline.Token))
                throw new InvalidDataException("MSI publisher, product family or version is not trusted.");
            deadline.Token.ThrowIfCancellationRequested();
            if (_clock.GetUtcNow() >= release.ExpiresAt || platform.InstalledVersion != installed)
                throw new InvalidOperationException("Release expired or installed version changed before installation.");

            // Persist the nonce before starting MSI: an interrupted/failed install needs IT review,
            // rather than repeatedly interrupting employees with the same release.
            await using (var attempt = new FileStream(attemptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(attempt, new { release.ReleaseId, release.Version, StartedAt = _clock.GetUtcNow() }, cancellationToken: cancellationToken);
            await RecordAsync("Installing", release);
            // MSI may stop the service. This independent runner must observe the transaction;
            // cancellation must never kill an already-started Windows Installer operation.
            int exitCode;
            bool serviceRunning;
            try { exitCode = await platform.InstallAsync(packagePath); }
            finally { serviceRunning = await platform.RestoreServiceAsync(); }
            var versionMatches = platform.InstalledVersion == target;
            var state = exitCode is 0 or 3010 && versionMatches && serviceRunning
                ? exitCode == 3010 ? "RestartRequired" : "Installed"
                : "InstallFailed";
            return await RecordAsync(state, release, exitCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or CryptographicException or
            InvalidOperationException or OperationCanceledException or UnauthorizedAccessException)
        {
            var failureReason = exception switch
            {
                InvalidDataException => exception.Message,
                HttpRequestException => "HTTPS request failed or returned a non-success status; redirects are disabled.",
                OperationCanceledException => "Update preparation was cancelled or timed out before installation.",
                UnauthorizedAccessException => "Updater file access was denied.",
                _ => "Updater storage or installation operation failed; IT must inspect the device and Windows Installer events."
            };
            await RecordAsync("Failed", release, failureReason: failureReason);
            throw;
        }
        finally
        {
            if (packagePath is not null && File.Exists(packagePath)) File.Delete(packagePath);
        }
    }

    private async Task DownloadAsync(string url, Stream destination, long limit, long? expected, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        // The production client disables redirects; never interpret a redirect body as a release.
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } length && (length > limit || expected is { } size && length != size))
            throw new InvalidDataException("Release download size is invalid.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) != 0)
        {
            total += read;
            if (total > limit) throw new InvalidDataException("Release download exceeded its size limit.");
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (expected is { } required && total != required) throw new InvalidDataException("Release download was truncated.");
    }

    private async Task<string> RecordAsync(string state, AgentRelease? release, int? installerExitCode = null, string? failureReason = null)
    {
        var entry = JsonSerializer.Serialize(new
        {
            At = _clock.GetUtcNow(),
            Actor = "Windows SYSTEM update task",
            State = state,
            release?.ReleaseId,
            release?.Version,
            release?.Reason,
            InstallerExitCode = installerExitCode,
            FailureReason = failureReason
        });
        await File.AppendAllTextAsync(Path.Combine(directory, "update-audit.jsonl"), entry + Environment.NewLine, CancellationToken.None);
        var temporary = Path.Combine(directory, "update-status.tmp");
        await File.WriteAllTextAsync(temporary, entry, CancellationToken.None);
        File.Move(temporary, Path.Combine(directory, "update-status.json"), overwrite: true);
        return state;
    }
}
