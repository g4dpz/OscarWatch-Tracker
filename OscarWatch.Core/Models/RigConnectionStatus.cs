namespace OscarWatch.Core.Models;

public sealed record RigConnectionStatus(
    bool IsConnected,
    bool IsTracking,
    RigStatusKind StatusKind,
    string? StatusPort,
    string? StatusDetail,
    long? LastReceiveHz,
    long? LastTransmitHz,
    bool CatUpdatesPaused = false,
    double ManualReceiveAdjustKHz = 0,
    double ManualTransmitAdjustKHz = 0);
