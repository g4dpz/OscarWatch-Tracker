namespace OscarWatch.Core.Models;

public sealed class TleSourceSettings
{
    public TleSourceMode Mode { get; set; } = TleSourceMode.OscarWatch;

    /// <summary>HTTP(S) URL when <see cref="Mode"/> is <see cref="TleSourceMode.CustomUrl"/>.</summary>
    public string CustomUrl { get; set; } = "";

    /// <summary>Path to a local two-line TLE file when <see cref="Mode"/> is <see cref="TleSourceMode.LocalFile"/>.</summary>
    public string LocalFilePath { get; set; } = "";
}
