namespace OscarWatch.Core.Models;

public sealed record RigConnectionStatus(
    bool IsConnected,
    bool IsTracking,
    string? StatusMessage,
    long? LastReceiveHz,
    long? LastTransmitHz,
    bool CatUpdatesPaused = false,
    double ManualReceiveAdjustKHz = 0,
    double ManualTransmitAdjustKHz = 0);
