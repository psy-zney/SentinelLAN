using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SentinelLAN.Infrastructure;

public sealed class SensitiveDataModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => context is SentinelDbContext sentinel
        ? (context.GetType(), sentinel.EncryptionModelKey, designTime) : (object)(context.GetType(), designTime);
}
