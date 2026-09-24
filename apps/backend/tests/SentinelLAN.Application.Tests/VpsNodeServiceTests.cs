using SentinelLAN.Application;
using SentinelLAN.Domain;
using Xunit;

namespace SentinelLAN.Application.Tests;

public sealed class VpsNodeServiceTests
{
    private const string HostFingerprint = "SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private sealed class InMemoryVpsNodeStore : IVpsNodeStore
    {
        public List<VpsNode> Nodes { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public HashSet<Guid> ReservedNonces { get; } = [];

        public Task<IReadOnlyList<VpsNode>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VpsNode>>(Nodes.Where(n => n.OrganizationId == organizationId).ToList());

        public Task<VpsNode?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Nodes.FirstOrDefault(n => n.OrganizationId == organizationId && n.Id == id));

        public Task<VpsNode> CreateAsync(VpsNode node, CancellationToken cancellationToken = default)
        {
            Nodes.Add(node);
            return Task.FromResult(node);
        }

        public Task<VpsNode> UpdateAsync(VpsNode node, CancellationToken cancellationToken = default)
        {
            var idx = Nodes.FindIndex(n => n.Id == node.Id);
            if (idx >= 0) Nodes[idx] = node;
            return Task.FromResult(node);
        }

        public Task<bool> DeleteAsync(Guid organizationId, Guid id, CancellationToken cancellationToken = default)
        {
            var removed = Nodes.RemoveAll(n => n.OrganizationId == organizationId && n.Id == id) > 0;
            return Task.FromResult(removed);
        }

        public Task<bool> ExistsNameAsync(Guid organizationId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            var exists = Nodes.Any(n => n.OrganizationId == organizationId &&
                                        n.Id != (excludeId ?? Guid.Empty) &&
                                        n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(exists);
        }

        public Task RecordAuditAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
        {
            Audits.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task<bool> TryReserveActionAsync(VpsActionReservation reservation, CancellationToken cancellationToken = default) =>
            Task.FromResult(ReservedNonces.Add(reservation.Nonce));
    }

    private sealed class MockVpsVaultService : IVpsVaultService
    {
        public string Encrypt(string plainText) => "ENC:" + plainText;
        public string Decrypt(string cipherText) => cipherText.StartsWith("ENC:", StringComparison.Ordinal) ? cipherText[4..] : cipherText;
    }

    private sealed class MockVpsSshService : IVpsSshService
    {
        public bool ShouldSucceed { get; set; } = true;
        public string? LastExecutedService { get; private set; }
        public int ExecutionCount { get; private set; }

        public Task<VpsConnectionTestResultDto> TestConnectionAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ShouldSucceed
                ? new VpsConnectionTestResultDto(true, "Connected", "Linux 6.8.0-generic", "up 5 days", 12.5, 45.0, 35.0, 3)
                : new VpsConnectionTestResultDto(false, "Connection refused"));
        }

        public Task<VpsMetricsResultDto> CollectMetricsAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ShouldSucceed
                ? new VpsMetricsResultDto(true, "Metrics OK", "Linux 6.8.0-generic", "up 5 days", 15.0, 48.0, 36.0, 4)
                : new VpsMetricsResultDto(false, "Probe failed"));
        }

        public Task<VpsCommandResultDto> RestartServiceAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, string serviceName, CancellationToken cancellationToken = default)
        {
            LastExecutedService = serviceName;
            ExecutionCount++;
            return Task.FromResult(ShouldSucceed
                ? new VpsCommandResultDto(true, $"Service '{serviceName}' restarted.")
                : new VpsCommandResultDto(false, $"Failed to restart '{serviceName}'."));
        }
    }

    [Fact]
    public async Task CreateNodeAsyncValidRequestEncryptsKeyAndRecordsAudit()
    {
        var store = new InMemoryVpsNodeStore();
        var vault = new MockVpsVaultService();
        var ssh = new MockVpsSshService();
        var service = new VpsNodeService(store, vault, ssh);

        var orgId = Guid.NewGuid();
        var actor = new ActorContext(Guid.NewGuid(), orgId, "Admin");
        const string rawKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nsecret_key_content\n-----END OPENSSH PRIVATE KEY-----";

        var request = new CreateVpsNodeRequest("Hanoi-VPS-01", "103.14.20.1", 22, "root", rawKey, HostFingerprint);
        var result = await service.CreateNodeAsync(actor, request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Hanoi-VPS-01", result.Name);
        Assert.Equal("103.14.20.1", result.Host);
        Assert.Equal("Online", result.Status);

        // Verify stored entity has encrypted key, not raw key
        var stored = store.Nodes.Single();
        Assert.NotEqual(rawKey, stored.EncryptedPrivateKey);
        Assert.Equal(rawKey, vault.Decrypt(stored.EncryptedPrivateKey));

        // Verify audit log
        var audit = store.Audits.Single(a => a.Action == "VpsNodeCreated");
        Assert.Equal(actor.UserId, audit.ActorId);
        Assert.Equal(orgId, audit.OrganizationId);
    }

    [Fact]
    public async Task CreateNodeAsyncDuplicateNameReturnsNull()
    {
        var store = new InMemoryVpsNodeStore();
        var vault = new MockVpsVaultService();
        var ssh = new MockVpsSshService();
        var service = new VpsNodeService(store, vault, ssh);

        var orgId = Guid.NewGuid();
        var actor = new ActorContext(Guid.NewGuid(), orgId, "Admin");
        const string rawKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nkey\n-----END OPENSSH PRIVATE KEY-----";

        store.Nodes.Add(new VpsNode
        {
            OrganizationId = orgId,
            Name = "Existing-Node",
            Host = "10.0.0.1",
            Port = 22,
            Username = "root",
            EncryptedPrivateKey = vault.Encrypt(rawKey)
        });

        var request = new CreateVpsNodeRequest("Existing-Node", "10.0.0.2", 22, "root", rawKey, HostFingerprint);
        var result = await service.CreateNodeAsync(actor, request, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RestartServiceAsyncDisallowedServiceFailsWithoutExecutingSsh()
    {
        var store = new InMemoryVpsNodeStore();
        var vault = new MockVpsVaultService();
        var ssh = new MockVpsSshService();
        var service = new VpsNodeService(store, vault, ssh);

        var orgId = Guid.NewGuid();
        var actor = new ActorContext(Guid.NewGuid(), orgId, "Admin");

        var node = new VpsNode
        {
            OrganizationId = orgId,
            Name = "Web-Node",
            Host = "10.0.0.1",
            Port = 22,
            Username = "root",
            EncryptedPrivateKey = vault.Encrypt("-----BEGIN OPENSSH PRIVATE KEY-----\nk\n-----END OPENSSH PRIVATE KEY-----")
        };
        store.Nodes.Add(node);

        var request = new RestartVpsServiceRequest("malicious_script.sh", "Maintenance", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
        var result = await service.RestartServiceAsync(actor, node.Id, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not in the allow-list", result.Message);
        Assert.Null(ssh.LastExecutedService);
    }

    [Fact]
    public async Task RestartServiceAsyncAllowedServiceExecutesAndAudits()
    {
        var store = new InMemoryVpsNodeStore();
        var vault = new MockVpsVaultService();
        var ssh = new MockVpsSshService();
        var service = new VpsNodeService(store, vault, ssh);

        var orgId = Guid.NewGuid();
        var actor = new ActorContext(Guid.NewGuid(), orgId, "Technician");

        var node = new VpsNode
        {
            OrganizationId = orgId,
            Name = "Nginx-Node",
            Host = "10.0.0.5",
            Port = 22,
            Username = "ubuntu",
            HostKeyFingerprint = HostFingerprint,
            EncryptedPrivateKey = vault.Encrypt("-----BEGIN OPENSSH PRIVATE KEY-----\nk\n-----END OPENSSH PRIVATE KEY-----")
        };
        store.Nodes.Add(node);

        var request = new RestartVpsServiceRequest("nginx", "Recover 502 bad gateway", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
        var result = await service.RestartServiceAsync(actor, node.Id, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("nginx", ssh.LastExecutedService);

        var audit = store.Audits.Single(a => a.Action == "VpsServiceRestarted");
        Assert.Equal("Success", audit.Outcome);
        Assert.Contains("Recover 502", audit.Reason);
    }

    [Fact]
    public async Task LegacyNodeWithoutHostKeyPinCannotExecuteSsh()
    {
        var store = new InMemoryVpsNodeStore();
        var ssh = new MockVpsSshService();
        var service = new VpsNodeService(store, new MockVpsVaultService(), ssh);
        var orgId = Guid.NewGuid();
        var actor = new ActorContext(Guid.NewGuid(), orgId, "Admin");
        var node = new VpsNode
        {
            OrganizationId = orgId,
            Name = "Old node",
            Host = "10.0.0.9",
            Username = "operator",
            EncryptedPrivateKey = "ENC:key"
        };
        store.Nodes.Add(node);

        var connection = await service.TestConnectionAsync(actor, node.Id, CancellationToken.None);
        var restart = await service.RestartServiceAsync(actor, node.Id, new RestartVpsServiceRequest("nginx", "Recovery", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2)), CancellationToken.None);

        Assert.False(connection.Success);
        Assert.False(restart.Success);
        Assert.Null(ssh.LastExecutedService);
    }

    [Fact]
    public async Task RestartWithoutConfirmationDoesNotExecuteSsh()
    {
        var store = new InMemoryVpsNodeStore();
        var ssh = new MockVpsSshService();
        var service = new VpsNodeService(store, new MockVpsVaultService(), ssh);
        var actor = new ActorContext(Guid.NewGuid(), Guid.NewGuid(), "Technician");

        var result = await service.RestartServiceAsync(actor, Guid.NewGuid(),
            new RestartVpsServiceRequest("nginx", "Recovery", false, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("confirmation", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(ssh.LastExecutedService);
    }

    [Fact]
    public async Task ReplayedRestartNonceIsRejectedBeforeSecondSshCommand()
    {
        var store = new InMemoryVpsNodeStore();
        var ssh = new MockVpsSshService();
        var service = new VpsNodeService(store, new MockVpsVaultService(), ssh);
        var actor = new ActorContext(Guid.NewGuid(), Guid.NewGuid(), "Technician");
        var node = new VpsNode
        {
            OrganizationId = actor.OrganizationId,
            Name = "Pinned VPS",
            Host = "10.0.0.5",
            Username = "operator",
            HostKeyFingerprint = HostFingerprint,
            EncryptedPrivateKey = "ENC:key"
        };
        store.Nodes.Add(node);
        var request = new RestartVpsServiceRequest("nginx", "Recover service", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));

        var first = await service.RestartServiceAsync(actor, node.Id, request, CancellationToken.None);
        var replay = await service.RestartServiceAsync(actor, node.Id, request, CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(replay.Success);
        Assert.Equal(1, ssh.ExecutionCount);
    }

    [Fact]
    public async Task ExpiredRestartRequestIsRejected()
    {
        var store = new InMemoryVpsNodeStore();
        var ssh = new MockVpsSshService();
        var service = new VpsNodeService(store, new MockVpsVaultService(), ssh);
        var actor = new ActorContext(Guid.NewGuid(), Guid.NewGuid(), "Technician");

        var result = await service.RestartServiceAsync(actor, Guid.NewGuid(),
            new RestartVpsServiceRequest("nginx", "Recover service", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(-1)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(store.ReservedNonces);
        Assert.Null(ssh.LastExecutedService);
    }
}
