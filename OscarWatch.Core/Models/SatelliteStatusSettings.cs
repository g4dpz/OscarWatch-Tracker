namespace OscarWatch.Core.Models;

public sealed class SatelliteStatusSettings
{
    public bool Enabled { get; set; }

    /// <summary>API host root, e.g. https://oscarwatch.org (no trailing slash required).</summary>
    public string BaseUrl { get; set; } = "https://oscarwatch.org";

    /// <summary>Sanctum personal access token (Bearer).</summary>
    public string ApiToken { get; set; } = "";

    /// <summary>When true, logging a live QSO submits an On status report for the focused satellite/mode.</summary>
    public bool AutoReportOnQso { get; set; }
}
