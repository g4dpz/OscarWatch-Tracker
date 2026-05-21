using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class DopplerPhysicsTests
{
    public static IEnumerable<object[]> DopplerRows()
    {
        foreach (var row in GoldenFixtureLoader.Load().Doppler)
            yield return new object[] { row };
    }

    [Theory]
    [MemberData(nameof(DopplerRows))]
    public void Nor_mode_matches_qtrig_rx_tx_physics(DopplerFixture row)
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = row.NominalHz / 1000.0,
            UplinkKHz = row.NominalHz / 1000.0,
            DownlinkMode = "USB",
            UplinkMode = "USB",
            Doppler = "NOR"
        };

        var rangeRateKmPerSec = row.RangeVelocityMps / 1000.0;
        var corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 0, 0);

        var expectedRx = row.RxHz / 1000.0;
        var expectedTx = row.TxHz / 1000.0;
        Assert.InRange(corrected.RadioReceiveKHz, expectedRx - 0.05, expectedRx + 0.05);
        Assert.InRange(corrected.RadioTransmitKHz, expectedTx - 0.05, expectedTx + 0.05);
    }

    [Fact]
    public void Rev_mode_inverts_doppler_signs_vs_nor()
    {
        const double downKhz = 435_850.45;
        const double upKhz = 145_952.65;
        const double rangeRateKmPerSec = 2.5;

        var nor = DopplerFrequencyCalculator.Compute(
            new SatelliteTransponderMode
            {
                DownlinkKHz = downKhz,
                UplinkKHz = upKhz,
                DownlinkMode = "USB",
                UplinkMode = "LSB",
                Doppler = "NOR"
            },
            rangeRateKmPerSec,
            0,
            0);

        var rev = DopplerFrequencyCalculator.Compute(
            new SatelliteTransponderMode
            {
                DownlinkKHz = downKhz,
                UplinkKHz = upKhz,
                DownlinkMode = "USB",
                UplinkMode = "LSB",
                Doppler = "REV"
            },
            rangeRateKmPerSec,
            0,
            0);

        Assert.NotEqual(nor.RadioReceiveKHz, rev.RadioReceiveKHz);
        Assert.NotEqual(nor.RadioTransmitKHz, rev.RadioTransmitKHz);
        Assert.True(rev.RadioReceiveKHz > downKhz);
        Assert.True(rev.RadioTransmitKHz < upKhz);
        Assert.True(nor.RadioReceiveKHz < downKhz);
        Assert.True(nor.RadioTransmitKHz > upKhz);
    }

    [Fact]
    public void Rev_tx_offset_lowers_radio_tx_on_inverting_satellite()
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var baseline = DopplerFrequencyCalculator.Compute(mode, 4.2, 0, 0);
        var withOffset = DopplerFrequencyCalculator.Compute(mode, 4.2, 9.275, 0);

        Assert.True(withOffset.RadioTransmitKHz < baseline.RadioTransmitKHz);
        Assert.InRange(withOffset.RadioTransmitKHz, 145_934.5, 145_936.5);
    }

    [Fact]
    public void Fo29_rev_tx_couples_downlink_doppler_for_inverting_passband()
    {
        const double downKhz = 435_850.45;
        const double upKhz = 145_952.65;
        const double shiftDownKhz = -6.814;
        const double rangeRateKmPerSec = -shiftDownKhz * 299_792.458 / downKhz;

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = downKhz,
            UplinkKHz = upKhz,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 4.0, 0);
        Assert.InRange(corrected.RadioReceiveKHz, 435_856.8, 435_857.5);
        Assert.InRange(corrected.RadioTransmitKHz, 145_938.8, 145_940.2);
    }

    [Fact]
    public void User_offsets_shift_radio_frequencies_directly()
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_667,
            UplinkKHz = 145_937,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var baseline = DopplerFrequencyCalculator.Compute(mode, 0, 0, 0);
        var rxPlus2 = DopplerFrequencyCalculator.Compute(mode, 0, 0, 2.5);
        var txPlus3 = DopplerFrequencyCalculator.Compute(mode, 0, 3.0, 0);

        Assert.Equal(baseline.RadioReceiveKHz - 2.5, rxPlus2.RadioReceiveKHz, 3);
        Assert.Equal(baseline.RadioTransmitKHz - 3.0, txPlus3.RadioTransmitKHz, 3);
    }

    [Fact]
    public void Fo29_rev_rx_matches_inverted_doppler_at_typical_range_rate()
    {
        const double downKhz = 435_850.45;
        const double upKhz = 145_952.65;
        const double rangeRateKmPerSec = 4.2;

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = downKhz,
            UplinkKHz = upKhz,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 0, 0);
        Assert.InRange(corrected.RadioReceiveKHz, 435_856.0, 435_857.5);
    }
}
