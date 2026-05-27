namespace OscarWatch.Core.Models;

public sealed class RigSettings
{
    public bool Enabled { get; set; }

    public RigType Type { get; set; } = RigType.None;

    public string Port { get; set; } = "";

    public int BaudRate { get; set; } = 19200;

    /// <summary>CI-V address as hex string (factory default for most ICOM rigs is 60).</summary>
    public string CivAddress { get; set; } = "60";

    public RigRegion Region { get; set; } = RigRegion.EU;

    public int DopplerThresholdFmHz { get; set; } = 50;

    public int DopplerThresholdLinearHz { get; set; } = 50;

    public int CatDelayMs { get; set; } = 50;

    /// <summary>When true, automatic CAT frequency updates are suspended (SatPC32-style).</summary>
    public bool CatUpdatesPaused { get; set; }

    /// <summary>
    /// When the frequency panel CW style is active: keep receive in USB/LSB from the database
    /// instead of setting downlink to CW.
    /// </summary>
    public bool CwKeepSidebandDownlink { get; set; }

    /// <summary>Factory CI-V address defaults (9700=A2, 9100/910=7C). User may still use 60.</summary>
    public static string DefaultCivAddressFor(RigType type) => type switch
    {
        RigType.IcomIc9700 => "A2",
        RigType.IcomIc9100 => "7C",
        RigType.IcomIc910 => "7C",
        _ => "60"
    };
}
