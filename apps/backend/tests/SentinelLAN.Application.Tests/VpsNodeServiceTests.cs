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

        public Task<IReadOnlyList<VpsNode>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VpsNode>>(Nodes.ToList());

        public Task<VpsNode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Nodes.FirstOrDefault(n => n.Id == id));

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

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var removed = Nodes.RemoveAll(n => n.Id == id) > 0;
            return Task.FromResult(removed);
        }

        public Task<bool> ExistsNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            var exists = Nodes.Any(n => n.Id != (excludeId ?? Guid.Empty) &&
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
        public VpsMetricsResultDto? Metrics { get; set; }
        public Action? BeforeOperation { get; set; }

        public Task<VpsConnectionTestResultDto> TestConnectionAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ShouldSucceed
                ? new VpsConnectionTestResultDto(true, "Connected", "Linux 6.8.0-generic", "up 5 days", 12.5, 45.0, 35.0, 3)
                : new VpsConnectionTestResultDto(false, "Connection refused"));
        }

        public Task<VpsMetricsResultDto> CollectMetricsAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Metrics ?? (ShouldSucceed
                ? new VpsMetricsResultDto(true, "Metrics OK", "Linux 6.8.0-generic", "up 5 days", 15.0, 48.0, 36.0, 4)
                : new VpsMetricsResultDto(false, "Probe failed")));
        }

        public Task<VpsCommandResultDto> ExecuteOperationAsync(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, VpsOperationRequest request, CancellationToken cancellationToken = default)
        {
            BeforeOperation?.Invoke();
            ExecutionCount++;
            return Task.FromResult(new VpsCommandResultDto(ShouldSucceed, ShouldSucceed ? "Applied" : "Failed"));
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
        var actor = new ActorContext(Guid.NewGuid(), orgId, Roles.PlatformOwner);
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
        var actor = new ActorContext(Guid.NewGuid(), orgId, Roles.PlatformOwner);
        const string rawKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nkey\n-----END OPENSSH PRIVATE KEY-----";

        store.Nodes.Add(new VpsNode
        {
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
        var actor = new ActorContext(Guid.NewGuid(), orgId, Roles.PlatformOwner);

        var node = new VpsNode
        {
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
        var actor = new ActorContext(Guid.NewGuid(), orgId, Roles.PlatformOwner);

        var node = new VpsNode
        {
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
        var actor = new ActorContext(Guid.NewGuid(), orgId, Roles.PlatformOwner);
        var node = new VpsNode
        {
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
        var actor = new ActorContext(Guid.NewGuid(), Guid.NewGuid(), Roles.PlatformOwner);

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
        var actor = new ActorContext(Guid.NewGuid(), Guid.NewGuid(), Roles.PlatformOwner);
        var node = new VpsNode
        {
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
        var actor = new ActorContext(Guid.NewGuid(), Guid.NewGuid(), Roles.PlatformOwner);

        var result = await service.RestartServiceAsync(actor, Guid.NewGuid(),
            new RestartVpsServiceRequest("nginx", "Recover service", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(-1)), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(store.ReservedNonces);
        Assert.Null(ssh.LastExecutedService);
    }

    private static (InMemoryVpsNodeStore Store, MockVpsSshService Ssh, VpsNodeService Service, VpsNode Node, ActorContext Actor) OperationsContext()
    {
        var store = new InMemoryVpsNodeStore();
        var ssh = new MockVpsSshService();
        var node = new VpsNode { Name = "Lab VPS", Host = "10.0.0.5", Username = "operator", HostKeyFingerprint = HostFingerprint, EncryptedPrivateKey = "ENC:key" };
        store.Nodes.Add(node);
        return (store, ssh, new(store, new MockVpsVaultService(), ssh), node, new(Guid.NewGuid(), Guid.NewGuid(), Roles.PlatformOwner));
    }

    [Theory]
    [InlineData("Reboot", null, null)]
    [InlineData("EnableServiceStartup", "docker", null)]
    [InlineData("DisableServiceStartup", "nginx", null)]
    [InlineData("SetContainerRestartPolicy", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "unless-stopped")]
    public async Task AllowedOperationReservesNonceAndAuditsBeforeExecution(string action, string? target, string? value)
    {
        var (store, ssh, service, node, actor) = OperationsContext();
        var request = new VpsOperationRequest(action, target, value, "Authorized maintenance", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
        ssh.BeforeOperation = () =>
        {
            Assert.Contains(request.Nonce, store.ReservedNonces);
            Assert.Equal("Accepted", Assert.Single(store.Audits).Outcome);
        };
        var first = await service.ExecuteOperationAsync(actor, node.Id, request, CancellationToken.None);
        var replay = await service.ExecuteOperationAsync(actor, node.Id, request, CancellationToken.None);
        Assert.True(first.Success);
        Assert.False(replay.Success);
        Assert.Equal(1, ssh.ExecutionCount);
        Assert.Equal(action == "Reboot" ? "Scheduled" : "Success", store.Audits[1].Outcome);
        Assert.Contains(node.Id.ToString(), store.Audits[0].Reason);
    }

    [Theory]
    [InlineData("RunShell", "ls", null)]
    [InlineData("EnableServiceStartup", "docker; reboot", null)]
    [InlineData("EnableServiceStartup", "DOCKER", null)]
    [InlineData("Reboot", "nginx", null)]
    [InlineData("SetContainerRestartPolicy", "--help", "always")]
    [InlineData("SetContainerRestartPolicy", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "always; reboot")]
    public async Task InvalidOperationCannotReserveOrExecute(string action, string? target, string? value)
    {
        var (store, ssh, service, node, actor) = OperationsContext();
        var result = await service.ExecuteOperationAsync(actor, node.Id, new(action, target, value, "Maintenance", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2)), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Empty(store.ReservedNonces);
        Assert.Equal(0, ssh.ExecutionCount);
    }

    [Theory]
    [InlineData(false, 2, "Maintenance")]
    [InlineData(true, -1, "Maintenance")]
    [InlineData(true, 6, "Maintenance")]
    [InlineData(true, 2, " ")]
    public async Task InvalidConfirmationCannotExecute(bool confirmed, int minutes, string reason)
    {
        var (store, ssh, service, node, actor) = OperationsContext();
        var result = await service.ExecuteOperationAsync(actor, node.Id, new("Reboot", null, null, reason, confirmed, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(minutes)), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Empty(store.ReservedNonces);
        Assert.Equal(0, ssh.ExecutionCount);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Technician")]
    [InlineData("Employee")]
    public async Task CompanyRolesCannotReadOrOperatePlatformVps(string role)
    {
        var (store, ssh, service, node, actor) = OperationsContext();
        actor = actor with { Role = role };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetNodesAsync(actor, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshMetricsAsync(actor, node.Id, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ExecuteOperationAsync(actor, node.Id, new("Reboot", null, null, "Maintenance", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2)), CancellationToken.None));
        Assert.Empty(store.Audits);
        Assert.Equal(0, ssh.ExecutionCount);
    }

    [Fact]
    public async Task FailedOperationIsAuditedAndCannotBeReplayed()
    {
        var (store, ssh, service, node, actor) = OperationsContext();
        ssh.ShouldSucceed = false;
        var request = new VpsOperationRequest("Reboot", null, null, "Maintenance", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
        Assert.False((await service.ExecuteOperationAsync(actor, node.Id, request, CancellationToken.None)).Success);
        Assert.False((await service.ExecuteOperationAsync(actor, node.Id, request, CancellationToken.None)).Success);
        Assert.Equal("Failed", store.Audits[1].Outcome);
        Assert.Equal(1, ssh.ExecutionCount);
    }

    [Fact]
    public async Task RuntimeIsReturnedWithFreshMetricsAndUnavailableValuesClearOldNumbers()
    {
        var (_, ssh, service, node, actor) = OperationsContext();
        node.CpuPercent = 80;
        node.DockerContainersCount = 4;
        var runtime = new VpsRuntimeDto("degraded", false, "Docker unavailable", [new("docker", "failed", "disabled")], []);
        ssh.Metrics = new(true, "Checked", Runtime: runtime);
        var result = await service.RefreshMetricsAsync(actor, node.Id, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(runtime, result.Runtime);
        Assert.Null(result.CpuPercent);
        Assert.Null(result.DockerContainersCount);
        Assert.NotNull(result.LastCheckedAt);
    }
}
