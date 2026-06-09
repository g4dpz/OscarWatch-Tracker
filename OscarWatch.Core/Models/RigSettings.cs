namespace OscarWatch.Core.Models;

public sealed class RigSettings
{
    /// <summary>Factory / Hamlib-style default CAT rate for FT-817 and FT-818 (menu #14).</summary>
    public const int Ft817818DefaultBaudRate = 4800;

    /// <summary>Factory CI-V USB default baud for IC-705 (must match radio menu).</summary>
    public const int Ic705DefaultBaudRate = 115200;

    /// <summary>Typical menu 031 CAT RATE for FT-991 / FT-991A (4800–38400 supported).</summary>
    public const int Ft991DefaultBaudRate = 38400;

    /// <summary>Typical menu CAT-1 RATE for FTX-1 series (4800–115200 supported).</summary>
    public const int Ftx1DefaultBaudRate = 38400;

    public static bool IsYaesuNewCatDualEndpoint(RigType type) =>
        type is RigType.YaesuFt991 or RigType.YaesuFt991a or RigType.YaesuFtx1;

    /// <summary>Typical CI-V baud for IC-706 series (must match radio menu).</summary>
    public const int Ic706SeriesDefaultBaudRate = 19200;

    public static bool IsIc706SeriesEndpoint(RigType type) =>
        type is RigType.IcomIc706 or RigType.IcomIc706Mkii or RigType.IcomIc706MkiiG;

    public bool Enabled { get; set; }

    /// <summary>When true, downlink and uplink use separate radios (<see cref="Downlink"/> / <see cref="Uplink"/>).</summary>
    public bool DualRadioEnabled { get; set; }

    public RigEndpointSettings Downlink { get; set; } = new();

    public RigEndpointSettings Uplink { get; set; } = new();

    public RigType Type { get; set; } = RigType.None;

    public string Port { get; set; } = "";

    public int BaudRate { get; set; } = 19200;

    /// <summary>CI-V address as hex string (factory default for most ICOM rigs is 60).</summary>
    public string CivAddress { get; set; } = "60";

    public RigRegion Region { get; set; } = RigRegion.EU;

    public int DopplerThresholdFmHz { get; set; } = 350;

    public int DopplerThresholdLinearHz { get; set; } = 50;

    public int CatDelayMs { get; set; } = 50;

    /// <summary>When true, CAT Doppler uses range rate at utc + half Receive/Transmit CatDelayMs (SatPC32-style lead).</summary>
    public bool DopplerCatLeadEnabled { get; set; }

    /// <summary>When true, automatic CAT frequency updates are suspended (SatPC32-style).</summary>
    public bool CatUpdatesPaused { get; set; }

    /// <summary>
    /// When the frequency panel CW style is active: keep receive in USB/LSB from the database
    /// instead of setting downlink to CW.
    /// </summary>
    public bool CwKeepSidebandDownlink { get; set; }

    public bool IsDualRadio => DualRadioEnabled;

    public bool IsDualRadioConfigured =>
        DualRadioEnabled && Downlink.IsConfigured && Uplink.IsConfigured;

    public static bool IsDualCapableEndpoint(RigType type) =>
        type is RigType.YaesuFt817 or RigType.YaesuFt818 or RigType.YaesuFtx1
            or RigType.YaesuFt991 or RigType.YaesuFt991a
            or RigType.IcomIc705 or RigType.IcomIc706 or RigType.IcomIc706Mkii or RigType.IcomIc706MkiiG;

    /// <summary>FT-817/818 are dual-radio only; move legacy single-radio config to the downlink endpoint.</summary>
    public void MigrateFt817818ToDualOnly()
    {
        if (DualRadioEnabled || Type is not (RigType.YaesuFt817 or RigType.YaesuFt818))
            return;

        DualRadioEnabled = true;
        Downlink.Type = Type;
        Downlink.Port = Port;
        Downlink.BaudRate = BaudRate > 0 ? BaudRate : Ft817818DefaultBaudRate;
        Downlink.Region = Region;
        Downlink.CatDelayMs = CatDelayMs;
        Type = RigType.None;
        Port = "";
    }

    /// <summary>Region for RX pass-init tone clear (dual downlink) or single-radio.</summary>
    public RigRegion ReceiveRegion() =>
        DualRadioEnabled ? Downlink.Region : Region;

    /// <summary>Region for uplink CTCSS (dual uplink) or single-radio.</summary>
    public RigRegion TransmitRegion() =>
        DualRadioEnabled ? Uplink.Region : Region;

    /// <summary>CAT delay for downlink / RX writes.</summary>
    public int ReceiveCatDelayMs() =>
        DualRadioEnabled ? Downlink.CatDelayMs : CatDelayMs;

    /// <summary>CAT delay for uplink / TX writes.</summary>
    public int TransmitCatDelayMs() =>
        DualRadioEnabled ? Uplink.CatDelayMs : CatDelayMs;

    /// <summary>Factory CI-V address defaults (9700=A2, 9100/910=7C). User may still use 60.</summary>
    public static string DefaultCivAddressFor(RigType type) => type switch
    {
        RigType.IcomIc9700 => "A2",
        RigType.IcomIc9100 => "7C",
        RigType.IcomIc910 => "7C",
        RigType.IcomIc705 => "A4",
        RigType.IcomIc706 => "48",
        RigType.IcomIc706Mkii => "4C",
        RigType.IcomIc706MkiiG => "58",
        _ => "60"
    };
}
