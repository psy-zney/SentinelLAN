using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SentinelLAN.Infrastructure;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SentinelDbContext>
{
    public SentinelDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SentinelDbContext>()
            .UseNpgsql("Host=localhost;Database=sentinellan_design;Username=sentinellan;Password=design-time-only")
            .Options;
        return new SentinelDbContext(options);
    }
}
