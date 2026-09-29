using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface IQrCodeGenerator
{
    string GenerateOpaqueCode();
}
