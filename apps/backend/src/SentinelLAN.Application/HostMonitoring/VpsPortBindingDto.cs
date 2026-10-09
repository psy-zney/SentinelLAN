namespace SentinelLAN.Application;

public sealed record VpsPortBindingDto(int ContainerPort, string Protocol, string? HostIp, int? HostPort);
