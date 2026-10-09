namespace SentinelLAN.Application;

public sealed record VpsListeningPortDto(string Address, int Port, string Protocol, string? Process = null);
