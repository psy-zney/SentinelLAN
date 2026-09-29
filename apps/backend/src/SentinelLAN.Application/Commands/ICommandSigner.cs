using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public interface ICommandSigner
{
    string Sign(DeviceCommand command);
    bool Verify(DeviceCommand command);
}
