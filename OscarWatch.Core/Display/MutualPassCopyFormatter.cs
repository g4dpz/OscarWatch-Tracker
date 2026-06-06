using System.Globalization;
using System.Text;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Display;

public static class MutualPassCopyFormatter
{
    public sealed class Labels
    {
        public required string Title { get; init; }
        public required string Between { get; init; }
        public required string TimesIn { get; init; }
        public required string MutualWindowHeader { get; init; }
        public required string MutualWindowLine { get; init; }
        public required string YourPassHeader { get; init; }
        public required string RemotePassHeader { get; init; }
        public required string PassTimes { get; init; }
        public required string MaxElevation { get; init; }
        public required string Azimuth { get; init; }
    }

    public static string Format(
        MutualPassInfo pass,
        GroundStation localSite,
        GroundStation remoteSite,
        Labels labels,
        bool useUtc,
        ClockDisplayFormat clockFormat,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var localHeading = StationHeading(localSite);
        var remoteHeading = StationHeading(remoteSite);
        var mutualWindow = PassDisplayFormat.FormatPlannerAosLosLine(
            pass.MutualStartUtc,
            pass.MutualEndUtc,
            culture,
            useUtc,
            clockFormat);
        var overlap = PassDisplayFormat.FormatOverlapDurationPrecise(pass.Duration);

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(culture, labels.Title, pass.SatelliteName));
        sb.AppendLine(string.Format(culture, labels.Between, localHeading, remoteHeading));
        sb.AppendLine(string.Format(culture, labels.TimesIn, PassDisplayFormat.FormatTimeZoneLabel(useUtc)));
        sb.AppendLine();
        sb.AppendLine(labels.MutualWindowHeader);
        sb.AppendLine(string.Format(culture, labels.MutualWindowLine, mutualWindow, overlap));
        sb.AppendLine();
        AppendStationPass(sb, labels.YourPassHeader, localHeading, pass.LocalPass, labels, useUtc, clockFormat, culture);
        sb.AppendLine();
        AppendStationPass(sb, labels.RemotePassHeader, remoteHeading, pass.RemotePass, labels, useUtc, clockFormat, culture);

        return sb.ToString().TrimEnd();
    }

    private static void AppendStationPass(
        StringBuilder sb,
        string headerFormat,
        string stationHeading,
        PassInfo pass,
        Labels labels,
        bool useUtc,
        ClockDisplayFormat clockFormat,
        CultureInfo culture)
    {
        var passTimes = PassDisplayFormat.FormatPlannerAosLosLine(
            pass.AosUtc,
            pass.LosUtc,
            culture,
            useUtc,
            clockFormat);

        sb.AppendLine(string.Format(culture, headerFormat, stationHeading));
        sb.AppendLine(string.Format(culture, labels.PassTimes, passTimes));
        sb.AppendLine(string.Format(culture, labels.MaxElevation, pass.MaxElevationDeg));
        sb.AppendLine(string.Format(
            culture,
            labels.Azimuth,
            pass.AosAzimuthDeg,
            pass.LosAzimuthDeg));
    }

    private static string StationHeading(GroundStation site)
    {
        if (string.IsNullOrWhiteSpace(site.GridSquare))
            return site.DisplayName;

        return $"{site.DisplayName} ({site.GridSquare.ToUpperInvariant()})";
    }
}
