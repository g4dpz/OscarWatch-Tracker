namespace OscarWatch.Core.Models;

/// <summary>Live satellite/mode/frequency context from OscarWatch tracking for QSO prefill.</summary>
public sealed record LiveTrackerSnapshot(
    string SatelliteName,
    string Mode,
    string ModeRx,
    long UplinkHz,
    long DownlinkHz,
    string Band,
    string BandRx)
{
    public static LiveTrackerSnapshot Empty { get; } = new("", "", "", 0, 0, "", "");

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(SatelliteName)
        && (UplinkHz > 0 || DownlinkHz > 0);

    public string FrequencySummary
    {
        get
        {
            if (!IsAvailable)
                return "";

            var tx = FormatMHz(UplinkHz);
            var rx = FormatMHz(DownlinkHz);
            return string.Equals(tx, rx, StringComparison.Ordinal) ? tx : $"{tx} / {rx}";
        }
    }

    private static string FormatMHz(long hz)
    {
        if (hz <= 0)
            return "—";

        return (hz / 1_000_000.0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
    }
}
