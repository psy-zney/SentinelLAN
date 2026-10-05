using System.Net;
using System.Text;
using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent.Tests;

public sealed class EnrollmentFeedbackTests
{
    [Theory]
    [InlineData("TokenUsed")]
    [InlineData("TokenExpired")]
    [InlineData("unexpected server content")]
    public async Task EnrollmentMapsOnlyRecognizedErrorCodesWithoutExposingResponse(string code)
    {
        using var client = new HttpClient(new RejectedEnrollmentHandler(code)) { BaseAddress = new Uri("https://company.test") };
        var api = new AgentApi(client, "Authorized device");
        var failure = await Assert.ThrowsAsync<EnrollmentRejectedException>(() => api.EnrollAsync("one-time-secret", CancellationToken.None));
        Assert.Equal(code is "TokenUsed" or "TokenExpired" ? code : "EnrollmentRejected", failure.Code);
        Assert.DoesNotContain("one-time-secret", failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("untrusted-detail", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProgressFileReportsStateWithoutCredentials()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sentinellan-progress-{Guid.NewGuid():N}.json");
        try
        {
            var progress = new FileAgentEnrollmentProgress(path);
            await progress.ReportAsync("TokenUsed", CancellationToken.None);
            await progress.ReportAsync("Connected", CancellationToken.None);
            using var result = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal("Connected", result.RootElement.GetProperty("state").GetString());
            Assert.Equal(2, result.RootElement.EnumerateObject().Count());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private sealed class RejectedEnrollmentHandler(string code) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { code, detail = "untrusted-detail one-time-secret" }), Encoding.UTF8, "application/problem+json")
            });
    }
}
