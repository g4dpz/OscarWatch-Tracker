namespace OscarWatch.Core.Models;

/// <summary>One radio endpoint (downlink or uplink) in dual-radio mode.</summary>
public sealed class RigEndpointSettings
{
    public RigType Type { get; set; } = RigType.None;

    public string Port { get; set; } = "";

    public int BaudRate { get; set; } = RigSettings.Ft817818DefaultBaudRate;

    public RigRegion Region { get; set; } = RigRegion.EU;

    public int CatDelayMs { get; set; } = 50;

    /// <summary>CI-V address as hex string (IC-705 default A4).</summary>
    public string CivAddress { get; set; } = "";

    public bool IsConfigured =>
        RigSettings.IsDualCapableEndpoint(Type)
        && !string.IsNullOrWhiteSpace(Port);
}
