using System.Globalization;
using System.Text;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Logbook;

public static class AdifExporter
{
    public static string ExportLogbook(QsoLogbook logbook, IReadOnlyList<QsoRecord> qsos)
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        foreach (var qso in qsos.OrderBy(q => q.QsoUtc))
            AppendRecord(sb, logbook, qso);

        return sb.ToString();
    }

    public static string ExportRecord(QsoLogbook logbook, QsoRecord qso)
    {
        var sb = new StringBuilder();
        AppendRecord(sb, logbook, qso);
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb)
    {
        AppendField(sb, "PROGRAMID", "OscarWatch");
        AppendField(sb, "PROGRAMVERSION", typeof(AdifExporter).Assembly.GetName().Version?.ToString(3) ?? "1.0");
        sb.AppendLine("<EOH>");
    }

    private static void AppendRecord(StringBuilder sb, QsoLogbook logbook, QsoRecord qso)
    {
        AppendField(sb, "CALL", MaidenheadLocator.NormalizeCallsign(qso.Call));
        AppendField(sb, "QSO_DATE", QsoLogbookTime.NormalizeToUtc(qso.QsoUtc).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        AppendField(sb, "TIME_ON", QsoLogbookTime.NormalizeToUtc(qso.QsoUtc).ToString("HHmm", CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(qso.RstSent))
            AppendField(sb, "RST_SENT", qso.RstSent.Trim());
        if (!string.IsNullOrWhiteSpace(qso.RstRcvd))
            AppendField(sb, "RST_RCVD", qso.RstRcvd.Trim());
        if (!string.IsNullOrWhiteSpace(qso.GridSquare))
            AppendField(sb, "GRIDSQUARE", MaidenheadLocator.NormalizeGrids(qso.GridSquare));
        if (!string.IsNullOrWhiteSpace(qso.Name))
            AppendField(sb, "NAME", qso.Name.Trim());
        if (!string.IsNullOrWhiteSpace(qso.Comment))
            AppendField(sb, "COMMENT", qso.Comment.Trim());

        if (!string.IsNullOrWhiteSpace(logbook.MyCallsign))
            AppendField(sb, "STATION_CALLSIGN", MaidenheadLocator.NormalizeCallsign(logbook.MyCallsign));
        if (!string.IsNullOrWhiteSpace(logbook.MyGridSquare))
            AppendField(sb, "MY_GRIDSQUARE", MaidenheadLocator.NormalizeGrids(logbook.MyGridSquare));

        if (!string.IsNullOrWhiteSpace(qso.SatName))
            AppendField(sb, "SAT_NAME", qso.SatName.Trim());
        AppendField(sb, "PROP_MODE", string.IsNullOrWhiteSpace(qso.PropMode) ? "SAT" : qso.PropMode.Trim());

        if (qso.FreqHz > 0)
            AppendField(sb, "FREQ", FormatFreqMhz(qso.FreqHz));
        if (!string.IsNullOrWhiteSpace(qso.Mode))
            AppendField(sb, "MODE", qso.Mode.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(qso.Band))
            AppendField(sb, "BAND", qso.Band.Trim());

        if (qso.FreqRxHz > 0 && qso.FreqRxHz != qso.FreqHz)
            AppendField(sb, "FREQ_RX", FormatFreqMhz(qso.FreqRxHz));
        if (!string.IsNullOrWhiteSpace(qso.ModeRx) && !string.Equals(qso.Mode, qso.ModeRx, StringComparison.OrdinalIgnoreCase))
            AppendField(sb, "MODE_RX", qso.ModeRx.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(qso.BandRx) && !string.Equals(qso.Band, qso.BandRx, StringComparison.OrdinalIgnoreCase))
            AppendField(sb, "BAND_RX", qso.BandRx.Trim());

        sb.AppendLine("<EOR>");
    }

    private static string FormatFreqMhz(long hz) =>
        (hz / 1_000_000.0).ToString("0.000000", CultureInfo.InvariantCulture);

    internal static void AppendField(StringBuilder sb, string tag, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var data = EscapeAdifValue(value);
        sb.Append('<').Append(tag).Append(':').Append(data.Length).Append('>').Append(data);
    }

    internal static string EscapeAdifValue(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("<", "\\<", StringComparison.Ordinal);
}
