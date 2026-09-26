using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class SelfServiceCommandTests
{
    private static readonly byte[] PackageBytes = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1, .. new byte[1016]];
    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(PackageBytes));
    private const string Publisher = "0123456789ABCDEF0123456789ABCDEF01234567";
    private static readonly Guid DeviceId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Theory]
    [InlineData("InstallApprovedApp")]
    [InlineData("PauseAgent")]
    [InlineData("UninstallAgent")]
    public void SelfServiceCommandEnvelopeStillRequiresSignatureDeviceExpiryAndUniqueNonce(string type)
    {
        var command = Command(type);
        var verifier = new CommandVerifier();
        Assert.False(verifier.TryAccept(command, Guid.NewGuid(), _now, _ => true, out _));
        Assert.False(verifier.TryAccept(command, DeviceId, _now, _ => false, out _));
        Assert.False(verifier.TryAccept(command with { ExpiresAt = _now.AddSeconds(-1) }, DeviceId, _now, _ => true, out _));
        Assert.True(verifier.TryAccept(command, DeviceId, _now, _ => true, out _));
        Assert.False(verifier.TryAccept(command, DeviceId, _now, _ => true, out var reason));
        Assert.Contains("Replay", reason);
    }

    [Theory]
    [InlineData("http://packages.example.test/app.msi")]
    [InlineData("https://packages.example.test/app.exe")]
    [InlineData("https://packages.example.test:444/app.msi")]
    [InlineData("https://person:password@packages.example.test/app.msi")]
    [InlineData("https://packages.example.test/app.msi?run=script")]
    [InlineData("https://127.0.0.1/app.msi")]
    public void InstallerParametersRejectUnboundedOrNonMsiUrls(string url)
    {
        Assert.False(SelfServiceCommandParameters.TryReadApp(AppCommand(url), _now, out _));
    }

    [Fact]
    public void ParametersRejectExpiredApprovalArbitraryFieldsAndLongPrivilegeWindow()
    {
        Assert.False(SelfServiceCommandParameters.TryReadApp(AppCommand() with { Parameter = AppParameter(_now) }, _now, out _));
        Assert.False(SelfServiceCommandParameters.TryReadApp(AppCommand() with { Parameter = AppParameter(_now.AddMinutes(31)) }, _now, out _));
        var document = JsonSerializer.Serialize(new { requestId = Guid.NewGuid(), packageUrl = "https://packages.example.test/app.msi", sha256 = Hash,
            publisherThumbprint = Publisher, approvalExpiresAt = _now.AddMinutes(20), arguments = "powershell.exe" });
        Assert.False(SelfServiceCommandParameters.TryReadApp(AppCommand() with { Parameter = document }, _now, out _));
    }

    [Theory]
    [InlineData(false, true, "packages.example.test")]
    [InlineData(true, false, "packages.example.test")]
    [InlineData(true, true, "untrusted.example.test")]
    public async Task InstallRequiresLocalOptInElevationAndExactTrustedHost(bool optIn, bool elevated, string host)
    {
        var platform = new MsiFake { CanInstall = elevated };
        var transport = new PackageHandler(PackageBytes);
        using var http = new HttpClient(transport);
        var installer = new ApprovedAppInstaller(new(AllowApprovedAppInstall: optIn, TrustedPackageHosts: [host]),
            TempDirectory(), platform, http, new TestClock(_now));
        Assert.False((await installer.InstallAsync(AppCommand(), default)).Succeeded);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(0, platform.InstallCalls);
    }

    [Theory]
    [InlineData(false, true, "SHA256")]
    [InlineData(true, false, "publisher")]
    public async Task InstallRejectsHashAndPublisherMismatchWithoutExecutingMsi(bool correctHash, bool validPublisher, string evidence)
    {
        var path = TempDirectory();
        try
        {
            var platform = new MsiFake { ValidPublisher = validPublisher };
            using var http = new HttpClient(new PackageHandler(PackageBytes));
            var installer = new ApprovedAppInstaller(Options(), path, platform, http, new TestClock(_now));
            var command = AppCommand() with { Parameter = AppParameter(_now.AddMinutes(20), hash: correctHash ? Hash : new string('0', 64)) };
            var result = await installer.InstallAsync(command, default);
            Assert.False(result.Succeeded);
            Assert.Contains(evidence, result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, platform.InstallCalls);
            Assert.Empty(Directory.GetFiles(path));
        }
        finally { DeleteTempDirectory(path); }
    }

    [Fact]
    public async Task ValidImmutableCatalogMsiInstallsOnceAndReportsObservedFailureTruthfully()
    {
        var path = TempDirectory();
        try
        {
            var platform = new MsiFake { InstallResult = new(false, "MSI failed with a fixture exit code") };
            using var http = new HttpClient(new PackageHandler(PackageBytes));
            var installer = new ApprovedAppInstaller(Options(), path, platform, http, new TestClock(_now));
            var result = await installer.InstallAsync(AppCommand(), default);
            Assert.False(result.Succeeded);
            Assert.Equal(platform.InstallResult, result);
            Assert.Equal(1, platform.InstallCalls);
            Assert.Equal(Publisher, platform.Publisher);
            Assert.Empty(Directory.GetFiles(path));
        }
        finally { DeleteTempDirectory(path); }
    }

    [Theory]
    [InlineData(302, 1024, false)]
    [InlineData(200, 1023, false)]
    [InlineData(200, 1024, true)]
    public async Task PackageDownloadRejectsRedirectsOversizeAndNonMsiContent(int status, long limit, bool invalidContent)
    {
        var path = TempDirectory();
        try
        {
            var bytes = invalidContent ? Encoding.UTF8.GetBytes("not an MSI") : PackageBytes;
            var platform = new MsiFake();
            using var http = new HttpClient(new PackageHandler(bytes, (HttpStatusCode)status));
            var installer = new ApprovedAppInstaller(Options() with { MaxPackageBytes = limit }, path, platform, http, new TestClock(_now));
            var command = AppCommand() with { Parameter = AppParameter(_now.AddMinutes(20), hash: Convert.ToHexString(SHA256.HashData(bytes))) };
            Assert.False((await installer.InstallAsync(command, default)).Succeeded);
            Assert.Equal(0, platform.InstallCalls);
        }
        finally { DeleteTempDirectory(path); }
    }

    [Fact]
    public async Task DownloadCancellationNeverReachesInstaller()
    {
        var platform = new MsiFake();
        var path = TempDirectory();
        using var http = new HttpClient(new PackageHandler(PackageBytes));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            var installer = new ApprovedAppInstaller(Options(), path, platform, http, new TestClock(_now));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => installer.InstallAsync(AppCommand(), cancellation.Token));
            Assert.Equal(0, platform.InstallCalls);
        }
        finally { DeleteTempDirectory(path); }
    }

    [Fact]
    public async Task MaintenanceRequiresLocalOptInAndUnexpiredBoundedApproval()
    {
        var state = new InMemoryAgentMaintenanceStateStore();
        var platform = new MsiFake();
        var disabled = new AgentMaintenanceExecutor(new(), state, platform, new TestClock(_now));
        Assert.False((await disabled.ExecuteAsync(MaintenanceCommand("PauseAgent"), default)).Succeeded);
        var enabled = new AgentMaintenanceExecutor(new(AllowAgentMaintenance: true), state, platform, new TestClock(_now));
        Assert.False((await enabled.ExecuteAsync(MaintenanceCommand("PauseAgent", _now), default)).Succeeded);
        Assert.False((await enabled.ExecuteAsync(MaintenanceCommand("PauseAgent", _now.AddMinutes(31)), default)).Succeeded);
        Assert.False((await enabled.ExecuteAsync(MaintenanceCommand("PauseAgent") with { Parameter = "{\"pauseMinutes\": 999}" }, default)).Succeeded);
        Assert.Null(state.Load(_now));
    }

    [Fact]
    public async Task ApprovedPauseSurvivesRestartExpiresAutomaticallyAndCannotRepeatAuthorization()
    {
        var path = TempDirectory();
        try
        {
            var filename = Path.Combine(path, "maintenance.dat");
            var store = new ProtectedMaintenanceStateStore(filename, "unit-test-only-maintenance-secret-32-bytes");
            var executor = new AgentMaintenanceExecutor(new(AllowAgentMaintenance: true), store, new MsiFake(), new TestClock(_now));
            var command = MaintenanceCommand("PauseAgent");
            Assert.True((await executor.ExecuteAsync(command, default)).Succeeded);
            var reloaded = new ProtectedMaintenanceStateStore(filename, "unit-test-only-maintenance-secret-32-bytes");
            Assert.Equal(command.Id, reloaded.Load(_now)!.CommandId);
            Assert.Equal(command.Signature, reloaded.Load(_now)!.Authorization!.Signature);
            Assert.Equal(_now.AddMinutes(15), reloaded.Load(_now)!.MaintenanceUntil);
            Assert.Null(reloaded.Load(_now.AddMinutes(15)));
            Assert.False((await executor.ExecuteAsync(command, default)).Succeeded);
            var bytes = File.ReadAllBytes(filename);
            Assert.DoesNotContain("PauseAgent", Encoding.UTF8.GetString(bytes));
            bytes[^1] ^= 0x80;
            File.WriteAllBytes(filename, bytes);
            Assert.ThrowsAny<Exception>(() => reloaded.Load(_now));
        }
        finally { DeleteTempDirectory(path); }
    }

    [Fact]
    public async Task UninstallRequiresRegisteredSentinelMsiAndPersistsAuthorizationBeforeStarting()
    {
        var store = new InMemoryAgentMaintenanceStateStore();
        var platform = new MsiFake { Product = null };
        var executor = new AgentMaintenanceExecutor(new(AllowAgentMaintenance: true), store, platform, new TestClock(_now));
        Assert.False((await executor.ExecuteAsync(MaintenanceCommand("UninstallAgent"), default)).Succeeded);
        Assert.Null(store.Load(_now));
        platform.Product = Guid.NewGuid();
        var command = MaintenanceCommand("UninstallAgent");
        platform.BeforeUninstall = () => Assert.Equal(command.Id, store.Load(_now)?.CommandId);
        Assert.True((await executor.ExecuteAsync(command, default)).Succeeded);
        Assert.Equal(1, platform.UninstallCalls);
        Assert.False((await executor.ExecuteAsync(command, default)).Succeeded);
    }

    [Fact]
    public async Task PauseRetainsHealthControlSuppressesWorkAndResumesAfterExpiry()
    {
        var clock = new TestClock(_now);
        var store = new InMemoryAgentMaintenanceStateStore();
        store.Save(new(Guid.NewGuid(), Guid.NewGuid(), "PauseAgent", _now.AddMinutes(5), _now.AddMinutes(15)));
        var api = new ApiFake { Command = Command("RestartService") with { Parameter = "docker" } };
        var executor = new ExecutorFake();
        var cycle = new AgentCycle(new(DeviceId, "fixture-device-secret"), new Collector(), api, new(), new Signatures(), clock,
            executor: executor, maintenanceStateStore: store);
        await cycle.RunAsync(default);
        Assert.Equal("PauseAgent", api.Telemetry!.MaintenanceAction);
        Assert.Equal(_now.AddMinutes(15), api.Telemetry.MaintenanceUntil);
        Assert.False(api.Result!.Succeeded);
        Assert.Equal(0, executor.Calls);
        clock.Now = _now.AddMinutes(15);
        api.Command = Command("RestartService") with { IssuedAt = clock.Now, ExpiresAt = clock.Now.AddMinutes(5), Parameter = "docker" };
        await cycle.RunAsync(default);
        Assert.Null(api.Telemetry!.MaintenanceUntil);
        Assert.Equal(1, executor.Calls);
    }

    [Fact]
    public async Task MaintenanceRedeemUsesDeviceAuthenticationAndDoesNotSendCredentialsInBody()
    {
        var handler = new RedeemHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") };
        var api = new AgentApi(client, "fixture-device");
        var requestId = Guid.NewGuid();
        await api.RedeemMaintenanceAsync(new(DeviceId, "fixture-secret"), requestId, "12345678", default);
        Assert.Equal("/api/v1/agent/self-service/maintenance/redeem", handler.Path);
        Assert.Equal(DeviceId.ToString(), handler.Device);
        Assert.Equal("fixture-secret", handler.Secret);
        Assert.DoesNotContain("fixture-secret", handler.Body);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(requestId, body.RootElement.GetProperty("requestId").GetGuid());
        Assert.Equal("12345678", body.RootElement.GetProperty("code").GetString());
        await Assert.ThrowsAsync<ArgumentException>(() => api.RedeemMaintenanceAsync(new(DeviceId, "fixture-secret"), requestId, "abcd1234", default));
    }

    private RemoteCommand Command(string type) => new(Guid.NewGuid(), DeviceId, type, "Authorized fixture request", Guid.NewGuid().ToString("N"),
        "fixture-signature", _now, _now.AddMinutes(5));
    private RemoteCommand AppCommand(string url = "https://packages.example.test/app.msi") => Command("InstallApprovedApp") with { Parameter = AppParameter(_now.AddMinutes(20), url) };
    private string AppParameter(DateTimeOffset expires, string url = "https://packages.example.test/app.msi", string? hash = null) =>
        JsonSerializer.Serialize(new { requestId = Guid.NewGuid(), packageUrl = url, sha256 = hash ?? Hash, publisherThumbprint = Publisher, approvalExpiresAt = expires });
    private RemoteCommand MaintenanceCommand(string type, DateTimeOffset? expires = null) => Command(type) with
    { Parameter = JsonSerializer.Serialize(new { requestId = Guid.NewGuid(), maintenanceExpiresAt = expires ?? _now.AddMinutes(5), pauseMinutes = 15 }) };
    private static AgentExecutionOptions Options() => new(AllowApprovedAppInstall: true, TrustedPackageHosts: ["packages.example.test"]);
    private static string TempDirectory() => Path.Combine(Path.GetTempPath(), "sentinellan-self-service-" + Guid.NewGuid().ToString("N"));
    private static void DeleteTempDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class MsiFake : IWindowsMsiOperations
    {
        public bool CanInstall { get; set; } = true;
        public bool ValidPublisher { get; set; } = true;
        public int InstallCalls { get; private set; }
        public int UninstallCalls { get; private set; }
        public string? Publisher { get; private set; }
        public Guid? Product { get; set; } = Guid.NewGuid();
        public Action? BeforeUninstall { get; set; }
        public ExecutionResult InstallResult { get; set; } = new(true, "Observed fixture MSI exit 0");
        public Task<bool> VerifyPublisherAsync(string path, string publisher, CancellationToken cancellationToken)
        { Assert.EndsWith(".msi", path); Assert.True(File.Exists(path)); Publisher = publisher; return Task.FromResult(ValidPublisher); }
        public Task<ExecutionResult> InstallAsync(string path, CancellationToken cancellationToken)
        { InstallCalls++; Assert.True(File.Exists(path)); return Task.FromResult(InstallResult); }
        public Guid? RegisteredSentinelProductCode() => Product;
        public Task<ExecutionResult> UninstallAsync(Guid productCode, CancellationToken cancellationToken)
        { BeforeUninstall?.Invoke(); UninstallCalls++; Assert.Equal(Product, productCode); return Task.FromResult(new ExecutionResult(true, "Observed fixture uninstall")); }
    }
    private sealed class PackageHandler(byte[] bytes, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(bytes), RequestMessage = request }); }
    }
    private sealed class RedeemHandler : HttpMessageHandler
    {
        public string? Path, Device, Secret, Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Path = request.RequestUri!.AbsolutePath; Device = request.Headers.GetValues("X-SentinelLAN-Device-Id").Single(); Secret = request.Headers.GetValues("X-SentinelLAN-Device-Secret").Single(); Body = await request.Content!.ReadAsStringAsync(cancellationToken); return new(HttpStatusCode.OK); }
    }
    private sealed class Collector : ITelemetryCollector { public TelemetrySnapshot Collect() => new(1, 2, 3, "fixture", "test"); }
    private sealed class Signatures : ICommandSignatureVerifier { public bool IsConfigured => true; public bool Verify(RemoteCommand command) => true; }
    private sealed class ExecutorFake : ICommandExecutor
    { public int Calls; public Task<ExecutionResult> ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken) { Calls++; return Task.FromResult(new ExecutionResult(true, "fixture")); } }
    private sealed class ApiFake : IAgentApi
    {
        public RemoteCommand? Command;
        public TelemetrySnapshot? Telemetry;
        public ExecutionResult? Result;
        public Task<DeviceIdentity> EnrollAsync(string token, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, CancellationToken cancellationToken) { Telemetry = telemetry; return Task.CompletedTask; }
        public Task SendHeartbeatAsync(DeviceIdentity identity, TelemetrySnapshot telemetry, string? idempotencyKey, CancellationToken cancellationToken) => SendHeartbeatAsync(identity, telemetry, cancellationToken);
        public Task<RemoteCommand?> PollCommandAsync(DeviceIdentity identity, CancellationToken cancellationToken) { var command = Command; Command = null; return Task.FromResult(command); }
        public Task SendResultAsync(DeviceIdentity identity, Guid commandId, ExecutionResult result, CancellationToken cancellationToken) { Result = result; return Task.CompletedTask; }
    }
}
