namespace OscarWatch.Core.Models;

public sealed class CloudlogSettings
{
    public bool Enabled { get; set; }

    /// <summary>Base URL, e.g. https://your.cloudlog.site (no trailing path).</summary>
    public string BaseUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    /// <summary>Radio identifier in Cloudlog CAT (default OscarWatch).</summary>
    public string RadioName { get; set; } = "OscarWatch";

    /// <summary>Minimum milliseconds between API posts when frequencies change.</summary>
    public int MinUpdateIntervalMs { get; set; } = 1000;

    /// <summary>Public slug of the logbook used for grid lookups (hams.at roves).</summary>
    public string LogbookPublicSlug { get; set; } = "";

    /// <summary>When true and a logbook is selected, hams.at roves show which grids you still need.</summary>
    public bool CheckRoveGrids { get; set; } = true;
}
