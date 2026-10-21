using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class AgentWakeSignal(Uri server) : IAgentWakeSignal
{
    private readonly Channel<bool> pending = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    public async Task RunAsync(DeviceIdentity identity, CancellationToken ct)
    {
        var url = new Uri(server, "/hubs/agent-wake");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var connection = new HubConnectionBuilder().WithUrl(url, options =>
                {
                    options.Transports = HttpTransportType.WebSockets; options.SkipNegotiation = true;
                    options.WebSocketConfiguration = socket =>
                    {
                        foreach (var header in AgentRequestSigner.Headers(identity, "GET", url.PathAndQuery, [])) socket.SetRequestHeader(header.Key, header.Value);
                    };
                }).WithAutomaticReconnect().Build();
                connection.On("wake-up", () => { pending.Writer.TryWrite(true); });
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
                await connection.StartAsync(ct);
                // Reconnect periodically with a fresh HMAC proof, even on a quiet network.
                using var refresh = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var interval = Task.Delay(TimeSpan.FromMinutes(5), refresh.Token);
                await Task.WhenAny(interval, closed.Task.WaitAsync(ct));
                await refresh.CancelAsync();
            }
            catch (Exception error) when (!ct.IsCancellationRequested && error is HttpRequestException or IOException or OperationCanceledException or System.Net.WebSockets.WebSocketException or InvalidOperationException or TimeoutException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException) { }
            if (!ct.IsCancellationRequested) await Task.Delay(TimeSpan.FromSeconds(15), ct);
        }
    }
    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(timeout);
        try { return await pending.Reader.ReadAsync(deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
    }
}
