namespace OscarWatch.Core.Models;

/// <summary>Machine-readable rotator connection state for diagnostics.</summary>
public enum RotatorConnectionKind
{
    Unknown,
    Disabled,
    NoPortSelected,
    Disconnected,
    Connected,
    ConnectFailed,
}
