using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class EnrollmentRecoveryStoreTests
{
    [Fact]
    public async Task RecoverySecretIsPersistedBeforeHttpAndSurvivesApiRecreation()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sentinel-enrollment-recovery-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "pending.dat");
        try
        {
            var handler = new LostResponseHandler(path);
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://company.test") };
            var attemptStore = new FileEnrollmentAttemptStore(path, new DevelopmentIdentityStore(path));
            var first = new AgentApi(client, "LAB-WINDOWS", attemptStore);
            await Assert.ThrowsAsync<HttpRequestException>(() => first.EnrollAsync(new string('A', 64), CancellationToken.None));
            var second = new AgentApi(client, "LAB-WINDOWS",
                new FileEnrollmentAttemptStore(path, new DevelopmentIdentityStore(path)));
            var identity = await second.EnrollAsync(new string('A', 64), CancellationToken.None);
            Assert.Equal(handler.DeviceId, identity.DeviceId);
            Assert.Equal(handler.Secrets[0], handler.Secrets[1]);
            var changedTokenSecret = await attemptStore.GetOrCreateSecretAsync(new string('B', 64), client.BaseAddress.AbsoluteUri, CancellationToken.None);
            Assert.NotEqual(identity.DeviceSecret, changedTokenSecret);
            await second.ConfirmEnrollmentStoredAsync(CancellationToken.None);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private sealed class LostResponseHandler(string pendingPath) : HttpMessageHandler
    {
        public Guid DeviceId { get; } = Guid.NewGuid();
        public List<string> Secrets { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.True(File.Exists(pendingPath));
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var secret = json.RootElement.GetProperty("deviceSecret").GetString()!;
            Assert.Equal(64, secret.Length);
            Secrets.Add(secret);
            if (Secrets.Count == 1) throw new HttpRequestException("Lost registration response");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { deviceId = DeviceId, deviceSecret = secret }) };
        }
    }
}
