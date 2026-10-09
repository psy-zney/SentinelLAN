namespace SentinelLAN.Application;

public sealed record VpsRuntimeDto(
    string SystemState,
    bool DockerAvailable,
    string? DockerError,
    IReadOnlyList<VpsServiceStateDto> Services,
    IReadOnlyList<VpsContainerDto> Containers);
