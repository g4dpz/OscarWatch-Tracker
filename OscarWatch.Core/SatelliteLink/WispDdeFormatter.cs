using System.Globalization;
using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.SatelliteLink;

/// <summary>Formats a Wisp32/GSC DDE-compatible string (SatPC32 output style).</summary>
public static class WispDdeFormatter
{
    private static readonly CultureInfo EuCulture = CultureInfo.GetCultureInfo("de-DE");

    public static string FormatNoSatellite() => SatelliteLinkMessage.NoSatelliteWispDde;

    public static string Format(
        string satelliteName,
        LookAngles? lookAngles,
        long uplinkHz,
        long downlinkHz,
        string uplinkMode,
        string downlinkMode)
    {
        var az = lookAngles?.AzimuthDeg ?? 0;
        var el = lookAngles?.ElevationDeg ?? 0;
        var rr = lookAngles?.RangeRateKmPerSec ?? 0;

        return string.Concat(
            satelliteName.Trim(),
            " AZ", FormatDecimal(az),
            " EL", FormatDecimal(el),
            " UP", uplinkHz.ToString(CultureInfo.InvariantCulture),
            " U", uplinkMode.Trim().ToUpperInvariant(),
            " DN", downlinkHz.ToString(CultureInfo.InvariantCulture),
            " D", downlinkMode.Trim().ToUpperInvariant(),
            " MA0,0",
            " RR", FormatDecimal(rr));
    }

    public static string FormatFromContext(RigTrackingContext context, long uplinkHz, long downlinkHz, string uplinkMode, string downlinkMode) =>
        Format(
            context.TrackState.Name,
            context.TrackState.LookAngles,
            uplinkHz,
            downlinkHz,
            CloudlogRadioMapper.MapMode(uplinkMode),
            CloudlogRadioMapper.MapMode(downlinkMode));

    private static string FormatDecimal(double value) =>
        value.ToString("0.########", EuCulture);
}
