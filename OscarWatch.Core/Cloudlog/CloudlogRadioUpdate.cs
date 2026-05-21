namespace OscarWatch.Core.Cloudlog;

/// <summary>Normalized state for Cloudlog API v2 <c>/index.php/api/radio</c>.</summary>
public sealed record CloudlogRadioUpdate(
    string SatelliteName,
    long UplinkHz,
    long DownlinkHz,
    string UplinkMode,
    string DownlinkMode)
{
    public string Signature =>
        $"{SatelliteName}|{UplinkHz}|{DownlinkHz}|{UplinkMode}|{DownlinkMode}";
}
