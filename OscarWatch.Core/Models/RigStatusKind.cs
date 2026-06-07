namespace OscarWatch.Core.Models;

/// <summary>Machine-readable rig connection state for UI localization.</summary>
public enum RigStatusKind
{
    None,
    Disconnected,
    Connected,
    CatPaused,
    Tracking,
    NoComPort,
    SelectDualComPorts,
    NotConnected,
    DualNotConnected,
}
