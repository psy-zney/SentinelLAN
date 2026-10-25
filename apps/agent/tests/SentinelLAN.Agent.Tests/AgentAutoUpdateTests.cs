using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class AgentAutoUpdateTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string TestPrivateKey = CreateKey();
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Package = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1, 1, 2, 3, 4];
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"sentinellan-update-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task DisabledOrUnprivilegedUpdaterNeverDownloadsOrInstalls()
    {
        var platform = new Platform();
        using var handler = new Downloads(Sign(Release()), Package);
        using var http = new HttpClient(handler);
        var updater = new AgentAutoUpdater(http, platform, _directory, new FixedClock());
        Assert.Equal("Disabled", await updater.RunAsync(Settings() with { Enabled = false }, CancellationToken.None));
        platform.CanUpdate = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => updater.RunAsync(Settings(), CancellationToken.None));
        Assert.Empty(handler.Requests);
        Assert.Equal(0, platform.InstallCount);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.9.0")]
    public async Task SameOrOlderReleaseNeverDownloadsAnMsi(string version)
    {
        var platform = new Platform();
        using var handler = new Downloads(Sign(Release() with { Version = version }), Package);
        using var http = new HttpClient(handler);
        Assert.Equal("UpToDate", await new AgentAutoUpdater(http, platform, _directory, new FixedClock()).RunAsync(Settings(), CancellationToken.None));
        Assert.Single(handler.Requests);
        Assert.Equal(0, platform.InstallCount);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("long-lived")]
    [InlineData("company")]
    [InlineData("origin")]
    [InlineData("http")]
    [InlineData("credentials")]
    [InlineData("query")]
    [InlineData("version")]
    [InlineData("size")]
    [InlineData("hash")]
    [InlineData("nonce")]
    [InlineData("schema")]
    public void SignedButOutOfScopeOrInvalidReleaseIsRejected(string scenario)
    {
        var release = scenario switch
        {
            "expired" => Release() with { ExpiresAt = Now },
            "future" => Release() with { PublishedAt = Now.AddHours(1) },
            "long-lived" => Release() with { ExpiresAt = Now.AddDays(8) },
            "company" => Release() with { CompanyServerUrl = "https://another-company.test" },
            "origin" => Release() with { PackageUrl = "https://attacker.test/agent.msi" },
            "http" => Release() with { PackageUrl = "http://updates.test/agent.msi" },
            "credentials" => Release() with { PackageUrl = "https://user:password@updates.test/agent.msi" },
            "query" => Release() with { PackageUrl = "https://updates.test/agent.msi?command=anything" },
            "version" => Release() with { Version = "256.0.0" },
            "size" => Release() with { SizeBytes = AgentReleaseVerifier.MaxPackageBytes + 1 },
            "hash" => Release() with { Sha256 = "not-a-hash" },
            "nonce" => Release() with { ReleaseId = Guid.Empty },
            _ => Release() with { SchemaVersion = 2 }
        };
        Assert.Throws<InvalidDataException>(() => AgentReleaseVerifier.Verify(Sign(release), Settings(), Now));
    }

    [Fact]
    public async Task AlteredManifestIsRejectedBeforeAnyMsiDownload()
    {
        var original = Sign(Release());
        using var envelope = JsonDocument.Parse(original);
        var altered = JsonSerializer.SerializeToUtf8Bytes(new
        {
            payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(Release() with { Version = "9.0.0" }, JsonOptions)),
            signature = envelope.RootElement.GetProperty("signature").GetString()
        });
        using var handler = new Downloads(altered, Package);
        using var http = new HttpClient(handler);
        var platform = new Platform();
        await Assert.ThrowsAsync<InvalidDataException>(() => new AgentAutoUpdater(http, platform, _directory, new FixedClock()).RunAsync(Settings(), CancellationToken.None));
        Assert.Single(handler.Requests);
        Assert.Equal(0, platform.InstallCount);
    }

    [Fact]
    public void WrongKeyAndWeakKeyAreRejected()
    {
        using var wrongKey = RSA.Create(3072);
        Assert.Throws<InvalidDataException>(() => AgentReleaseVerifier.Verify(Sign(Release()), Settings() with { PublicKeyPem = wrongKey.ExportSubjectPublicKeyInfoPem() }, Now));
        using var weak = RSA.Create(2048);
        var payload = JsonSerializer.SerializeToUtf8Bytes(Release(), JsonOptions);
        var envelope = JsonSerializer.SerializeToUtf8Bytes(new { payload = Convert.ToBase64String(payload), signature = Convert.ToBase64String(weak.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) });
        Assert.Throws<InvalidDataException>(() => AgentReleaseVerifier.Verify(envelope, Settings() with { PublicKeyPem = weak.ExportSubjectPublicKeyInfoPem() }, Now));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("truncated")]
    [InlineData("oversized")]
    [InlineData("publisher-or-family")]
    public async Task InvalidDownloadedPackageNeverReachesInstaller(string scenario)
    {
        var downloaded = scenario switch
        {
            "hash" => Package.Select(value => (byte)(value ^ 1)).ToArray(),
            "truncated" => Package[..^1],
            "oversized" => Package.Concat(new byte[] { 0 }).ToArray(),
            _ => Package
        };
        var platform = new Platform { PackageTrusted = scenario != "publisher-or-family" };
        using var handler = new Downloads(Sign(Release()), downloaded);
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => new AgentAutoUpdater(http, platform, _directory, new FixedClock()).RunAsync(Settings(), CancellationToken.None));
        Assert.Equal(0, platform.InstallCount);
        Assert.Empty(Directory.GetFiles(_directory, "*.msi"));
    }

    [Theory]
    [InlineData(302)]
    [InlineData(404)]
    public async Task RedirectOrMissingManifestNeverReachesInstaller(int status)
    {
        using var handler = new Downloads(Sign(Release()), Package) { ManifestStatus = (HttpStatusCode)status };
        using var http = new HttpClient(handler);
        var platform = new Platform();
        await Assert.ThrowsAsync<HttpRequestException>(() => new AgentAutoUpdater(http, platform, _directory, new FixedClock()).RunAsync(Settings(), CancellationToken.None));
        Assert.Single(handler.Requests);
        Assert.Equal(0, platform.InstallCount);
    }

    [Theory]
    [InlineData(0, true, true, "Installed")]
    [InlineData(3010, true, true, "RestartRequired")]
    [InlineData(1603, false, true, "InstallFailed")]
    [InlineData(0, false, true, "InstallFailed")]
    [InlineData(0, true, false, "InstallFailed")]
    public async Task InstallationReportsObservedVersionServiceAndExitCode(int exitCode, bool versionChanged, bool serviceRunning, string expected)
    {
        var platform = new Platform { ExitCode = exitCode, ChangeVersion = versionChanged, ServiceRunning = serviceRunning };
        var release = Release();
        using var handler = new Downloads(Sign(release), Package);
        using var http = new HttpClient(handler);
        var updater = new AgentAutoUpdater(http, platform, _directory, new FixedClock());
        Assert.Equal(expected, await updater.RunAsync(Settings(), CancellationToken.None));
        Assert.Equal(1, platform.InstallCount);
        Assert.True(platform.ServiceRestored);
        Assert.True(File.Exists(Path.Combine(_directory, $"attempt-{release.ReleaseId:N}.json")));
        using var status = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_directory, "update-status.json")));
        Assert.Equal(expected, status.RootElement.GetProperty("State").GetString());
        Assert.Equal(exitCode, status.RootElement.GetProperty("InstallerExitCode").GetInt32());
        Assert.Equal(2, File.ReadAllLines(Path.Combine(_directory, "update-audit.jsonl")).Length);
        Assert.Empty(Directory.GetFiles(_directory, "*.msi"));
        // A failed or interrupted release nonce cannot repeatedly reinstall the Agent.
        platform.Version = new Version(1, 0, 0);
        Assert.Equal("AlreadyAttempted", await updater.RunAsync(Settings(), CancellationToken.None));
        Assert.Equal(1, platform.InstallCount);
    }

    [Fact]
    public async Task CancellationBeforeInstallNeverStartsMsi()
    {
        using var cancellation = new CancellationTokenSource();
        var platform = new Platform { OnVerify = cancellation.Cancel };
        using var handler = new Downloads(Sign(Release()), Package);
        using var http = new HttpClient(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AgentAutoUpdater(http, platform, _directory, new FixedClock()).RunAsync(Settings(), cancellation.Token));
        Assert.Equal(0, platform.InstallCount);
    }

    [Fact]
    public async Task ReleaseExpiringDuringVerificationNeverStartsMsi()
    {
        var clock = new FixedClock();
        var release = Release();
        var platform = new Platform { OnVerify = () => clock.Current = release.ExpiresAt };
        using var handler = new Downloads(Sign(release), Package);
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AgentAutoUpdater(http, platform, _directory, clock).RunAsync(Settings(), CancellationToken.None));
        Assert.Equal(0, platform.InstallCount);
    }

    [Fact]
    public async Task DurableAttemptAndExclusiveLockPreventAnotherInstall()
    {
        var release = Release();
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, $"attempt-{release.ReleaseId:N}.json"), "{\"State\":\"Installing\"}");
        var platform = new Platform();
        using var handler = new Downloads(Sign(release), Package);
        using var http = new HttpClient(handler);
        Assert.Equal("AlreadyAttempted", await new AgentAutoUpdater(http, platform, _directory, new FixedClock()).RunAsync(Settings(), CancellationToken.None));
        Assert.Equal(0, platform.InstallCount);
        Assert.Single(handler.Requests);
        using var runLock = new FileStream(Path.Combine(_directory, "run.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        await Assert.ThrowsAsync<IOException>(() => new AgentAutoUpdater(http, platform, _directory, new FixedClock()).RunAsync(Settings(), CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task OversizedManifestAndUnknownMsiHeaderNeverReachInstaller()
    {
        var platform = new Platform();
        using var largeHandler = new Downloads(new byte[64 * 1024 + 1], Package);
        using var largeHttp = new HttpClient(largeHandler);
        await Assert.ThrowsAsync<InvalidDataException>(() => new AgentAutoUpdater(largeHttp, platform, _directory, new FixedClock()).RunAsync(Settings(), CancellationToken.None));
        var invalidPackage = new byte[12];
        using var headerHandler = new Downloads(Sign(Release() with { Sha256 = Convert.ToHexString(SHA256.HashData(invalidPackage)) }), invalidPackage);
        using var headerHttp = new HttpClient(headerHandler);
        await Assert.ThrowsAsync<InvalidDataException>(() => new AgentAutoUpdater(headerHttp, platform, _directory, new FixedClock()).RunAsync(Settings(), CancellationToken.None));
        Assert.Equal(0, platform.InstallCount);
    }

    [Fact]
    public async Task InstallerProcessFailureRestoresServiceAndKeepsAttemptForReview()
    {
        var platform = new Platform { ThrowDuringInstall = true };
        var release = Release();
        using var handler = new Downloads(Sign(release), Package);
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<IOException>(() => new AgentAutoUpdater(http, platform, _directory, new FixedClock()).RunAsync(Settings(), CancellationToken.None));
        Assert.True(platform.ServiceRestored);
        Assert.True(File.Exists(Path.Combine(_directory, $"attempt-{release.ReleaseId:N}.json")));
        using var status = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_directory, "update-status.json")));
        Assert.Equal("Failed", status.RootElement.GetProperty("State").GetString());
        Assert.False(string.IsNullOrWhiteSpace(status.RootElement.GetProperty("FailureReason").GetString()));
    }

    private static AgentRelease Release() => new(1, Guid.NewGuid(), "2.0.0", "https://updates.test/agent-2.0.0.msi",
        Convert.ToHexString(SHA256.HashData(Package)), Package.Length, Now.AddMinutes(-1), Now.AddDays(1), "https://company.test", "Fix Windows version detection");

    private static AgentUpdateSettings Settings()
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(TestPrivateKey);
        return new(true, "https://updates.test/stable.json", rsa.ExportSubjectPublicKeyInfoPem(), new string('A', 40), "https://company.test");
    }

    private static string CreateKey()
    {
        using var rsa = RSA.Create(3072);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    private static byte[] Sign(AgentRelease release)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(TestPrivateKey);
        var payload = JsonSerializer.SerializeToUtf8Bytes(release, JsonOptions);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            payload = Convert.ToBase64String(payload),
            signature = Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
        });
    }

    public void Dispose()
    {
        var fullPath = Path.GetFullPath(_directory);
        if (!fullPath.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullPath).StartsWith("sentinellan-update-test-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a path outside the test directory.");
        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, recursive: true);
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class Downloads(byte[] manifest, byte[] package) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        public HttpStatusCode ManifestStatus { get; init; } = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request.RequestUri!);
            var isManifest = request.RequestUri!.AbsolutePath.EndsWith(".json", StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(isManifest ? ManifestStatus : HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(isManifest ? manifest : package)
            });
        }
    }

    private sealed class Platform : IAgentUpdatePlatform
    {
        public bool CanUpdate { get; set; } = true;
        public Version? InstalledVersion => Version;
        public Version Version { get; set; } = new(1, 0, 0);
        public bool PackageTrusted { get; init; } = true;
        public int InstallCount { get; private set; }
        public bool ServiceRestored { get; private set; }
        public bool ServiceRunning { get; init; } = true;
        public bool ChangeVersion { get; init; } = true;
        public int ExitCode { get; init; }
        public bool ThrowDuringInstall { get; init; }
        public Action? OnVerify { get; init; }
        public Task<bool> VerifyPackageAsync(string path, AgentRelease release, string publisherThumbprint, CancellationToken cancellationToken)
        {
            OnVerify?.Invoke();
            return Task.FromResult(PackageTrusted);
        }
        public Task<int> InstallAsync(string path)
        {
            InstallCount++;
            if (ThrowDuringInstall) throw new IOException("Installer process could not be observed.");
            if (ChangeVersion) Version = new Version(2, 0, 0);
            return Task.FromResult(ExitCode);
        }
        public Task<bool> RestoreServiceAsync()
        {
            ServiceRestored = true;
            return Task.FromResult(ServiceRunning);
        }
    }
}
