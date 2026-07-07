namespace OscarWatch.Core.Models;

public sealed class SatelliteLinkSettings
{
    public const int DefaultPort = 7373;
    public const int DefaultUpdateIntervalMs = 1000;

    public bool Enabled { get; set; }

    public int Port { get; set; } = DefaultPort;

    /// <summary>When false, listen on 127.0.0.1 only; when true, listen on all interfaces.</summary>
    public bool AllowLanClients { get; set; }

    /// <summary>When true, send <c>** NO SATELLITE **</c> when elevation is at or below zero.</summary>
    public bool OnlyWhenInRange { get; set; }

    public int UpdateIntervalMs { get; set; } = DefaultUpdateIntervalMs;

    public static int NormalizePort(int port) => Math.Clamp(port, 1024, 65535);

    public static int NormalizeUpdateIntervalMs(int ms) => Math.Clamp(ms, 250, 60_000);
}
