using System.Security.Cryptography;
using System.Text;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class FileEnrollmentAttemptStore(string path, IDeviceIdentityStore protectedStore) : IEnrollmentAttemptStore
{
    public async Task<string> GetOrCreateSecretAsync(string token, string server, CancellationToken ct)
    {
        var binding = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(server + "\n" + token)).AsSpan(0, 16));
        var pending = await protectedStore.LoadAsync(ct);
        if (pending is not null && pending.DeviceId == binding) return pending.DeviceSecret;
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await protectedStore.SaveAsync(new DeviceIdentity(binding, secret), ct);
        return secret;
    }

    public Task ClearAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
