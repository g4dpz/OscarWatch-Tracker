namespace OscarWatch.Core.Models;

/// <summary>One radio endpoint (downlink or uplink) in dual-radio mode.</summary>
public sealed class RigEndpointSettings
{
    public const int SdrRigCtlDefaultPort = 4532;
    public const int SdrConnectRigCtlPort = 5454;
    public const string SdrRigCtlDefaultHost = "127.0.0.1";

    public RigType Type { get; set; } = RigType.None;

    public string Port { get; set; } = "";

    public int BaudRate { get; set; } = RigSettings.Ft817818DefaultBaudRate;

    public RigRegion Region { get; set; } = RigRegion.EU;

    public int CatDelayMs { get; set; } = 50;

    /// <summary>CI-V address as hex string (IC-705 default A4).</summary>
    public string CivAddress { get; set; } = "";

    /// <summary>rigctl TCP host when <see cref="Type"/> is <see cref="RigType.SdrRigCtlTcp"/>.</summary>
    public string NetworkHost { get; set; } = SdrRigCtlDefaultHost;

    /// <summary>rigctl TCP port when <see cref="Type"/> is <see cref="RigType.SdrRigCtlTcp"/>.</summary>
    public int NetworkPort { get; set; } = SdrRigCtlDefaultPort;

    public bool IsConfigured =>
        RigSettings.IsSdrDownlinkEndpoint(Type)
            ? NetworkPort is > 0 and <= 65535 && !string.IsNullOrWhiteSpace(NetworkHost)
            : RigSettings.IsDualCapableSerialEndpoint(Type) && !string.IsNullOrWhiteSpace(Port);
}
