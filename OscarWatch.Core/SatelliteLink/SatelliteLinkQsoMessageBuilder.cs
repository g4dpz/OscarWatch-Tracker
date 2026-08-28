using System.Globalization;
using OscarWatch.Core.Logbook;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.SatelliteLink;

public static class SatelliteLinkQsoMessageBuilder
{
    public static SatelliteLinkQsoMessage Build(
        QsoRecord record,
        QsoLogbook logbook,
        SatelliteLinkQsoEventKind kind,
        DateTime timestampUtc,
        string? noradId = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(logbook);

        return new SatelliteLinkQsoMessage
        {
            Type = SatelliteLinkQsoMessage.MapType(kind),
            TimestampUtc = FormatTimestamp(timestampUtc),
            Logbook = MapLogbook(logbook),
            Qso = kind == SatelliteLinkQsoEventKind.Deleted
                ? MapDeletedQso(record)
                : MapFullQso(record, noradId),
            Adif = kind == SatelliteLinkQsoEventKind.Deleted
                ? null
                : ExportAdifRecord(logbook, record)
        };
    }

    private static string ExportAdifRecord(QsoLogbook logbook, QsoRecord record) =>
        AdifExporter.ExportRecord(logbook, record, forLotw: false).TrimEnd('\r', '\n');

    private static SatelliteLinkQsoLogbookInfo MapLogbook(QsoLogbook logbook) => new()
    {
        Id = logbook.Id,
        Name = logbook.Name.Trim(),
        MyCallsign = logbook.MyCallsign.Trim(),
        MyGridSquare = logbook.MyGridSquare.Trim()
    };

    private static SatelliteLinkQsoInfo MapDeletedQso(QsoRecord record) => new()
    {
        Id = record.Id,
        Call = record.Call.Trim()
    };

    private static SatelliteLinkQsoInfo MapFullQso(QsoRecord record, string? noradId)
    {
        SatelliteLinkQsoSatelliteInfo? satellite = null;
        if (!string.IsNullOrWhiteSpace(record.SatName))
        {
            satellite = new SatelliteLinkQsoSatelliteInfo
            {
                Name = record.SatName.Trim(),
                NoradId = string.IsNullOrWhiteSpace(noradId) ? null : noradId.Trim()
            };
        }

        SatelliteLinkQsoFrequencyInfo? frequencies = null;
        SatelliteLinkBandInfo? bands = null;
        if (record.FreqHz > 0 || record.FreqRxHz > 0)
        {
            frequencies = new SatelliteLinkQsoFrequencyInfo
            {
                UplinkHz = record.FreqHz,
                DownlinkHz = record.FreqRxHz,
                UplinkMode = record.Mode.Trim(),
                DownlinkMode = record.ModeRx.Trim()
            };
            bands = new SatelliteLinkBandInfo
            {
                Tx = record.Band.Trim(),
                Rx = record.BandRx.Trim()
            };
        }

        return new SatelliteLinkQsoInfo
        {
            Id = record.Id,
            QsoUtc = FormatTimestamp(record.QsoUtc),
            Call = record.Call.Trim(),
            RstSent = NullIfEmpty(record.RstSent),
            RstRcvd = NullIfEmpty(record.RstRcvd),
            GridSquare = NullIfEmpty(record.GridSquare),
            Name = NullIfEmpty(record.Name),
            Comment = NullIfEmpty(record.Comment),
            Satellite = satellite,
            Frequencies = frequencies,
            Bands = bands,
            PropMode = string.IsNullOrWhiteSpace(record.PropMode) ? "SAT" : record.PropMode.Trim()
        };
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatTimestamp(DateTime timestampUtc) =>
        timestampUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
