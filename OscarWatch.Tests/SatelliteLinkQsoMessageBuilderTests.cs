using OscarWatch.Core.Models;
using OscarWatch.Core.SatelliteLink;

namespace OscarWatch.Tests;

public class SatelliteLinkQsoMessageBuilderTests
{
    private static QsoLogbook MakeLogbook() => new()
    {
        Id = 3,
        Name = "Field day",
        MyCallsign = "G0ABC",
        MyGridSquare = "IO91",
        CreatedUtc = DateTime.UtcNow
    };

    private static QsoRecord MakeRecord() => new()
    {
        Id = 42,
        LogbookId = 3,
        QsoUtc = new DateTime(2026, 7, 11, 14, 30, 0, DateTimeKind.Utc),
        Call = "DL1ABC",
        RstSent = "59",
        RstRcvd = "59",
        GridSquare = "JO62",
        SatName = "SO-50",
        Mode = "FM",
        ModeRx = "FM",
        FreqHz = 435_300_000,
        FreqRxHz = 145_850_000,
        Band = "70cm",
        BandRx = "2m",
        PropMode = "SAT",
        CreatedUtc = DateTime.UtcNow
    };

    [Fact]
    public void Build_logged_maps_qso_and_logbook_fields()
    {
        var msg = SatelliteLinkQsoMessageBuilder.Build(
            MakeRecord(),
            MakeLogbook(),
            SatelliteLinkQsoEventKind.Logged,
            new DateTime(2026, 7, 11, 14, 30, 5, DateTimeKind.Utc),
            noradId: "27607");

        Assert.Equal("qsoLogged", msg.Type);
        Assert.Equal(1, msg.Version);
        Assert.Equal("Field day", msg.Logbook!.Name);
        Assert.Equal("G0ABC", msg.Logbook.MyCallsign);
        Assert.Equal("IO91", msg.Logbook.MyGridSquare);
        Assert.Equal(42, msg.Qso!.Id);
        Assert.Equal("DL1ABC", msg.Qso.Call);
        Assert.Equal("2026-07-11T14:30:00.000Z", msg.Qso.QsoUtc);
        Assert.Equal("SO-50", msg.Qso.Satellite!.Name);
        Assert.Equal("27607", msg.Qso.Satellite.NoradId);
        Assert.Equal(435_300_000, msg.Qso.Frequencies!.UplinkHz);
        Assert.Equal(145_850_000, msg.Qso.Frequencies.DownlinkHz);
        Assert.Equal("70cm", msg.Qso.Bands!.Tx);
        Assert.Equal("2m", msg.Qso.Bands.Rx);
        Assert.Equal("SAT", msg.Qso.PropMode);
    }

    [Fact]
    public void Build_updated_uses_updated_type()
    {
        var msg = SatelliteLinkQsoMessageBuilder.Build(
            MakeRecord(),
            MakeLogbook(),
            SatelliteLinkQsoEventKind.Updated,
            DateTime.UtcNow);

        Assert.Equal("qsoUpdated", msg.Type);
        Assert.Equal("DL1ABC", msg.Qso!.Call);
    }

    [Fact]
    public void Build_deleted_emits_minimal_payload()
    {
        var msg = SatelliteLinkQsoMessageBuilder.Build(
            MakeRecord(),
            MakeLogbook(),
            SatelliteLinkQsoEventKind.Deleted,
            DateTime.UtcNow);

        Assert.Equal("qsoDeleted", msg.Type);
        Assert.Equal(42, msg.Qso!.Id);
        Assert.Equal("DL1ABC", msg.Qso.Call);
        Assert.Null(msg.Qso.QsoUtc);
        Assert.Null(msg.Qso.Satellite);
        Assert.Null(msg.Qso.Frequencies);
    }
}
