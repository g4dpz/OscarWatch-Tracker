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
        const double downKhz = 435_667;
        const double upKhz = 145_937.61;
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

        Assert.Equal(baseline.RadioReceiveKHz + 2.5, rxPlus2.RadioReceiveKHz, 3);
        Assert.Equal(baseline.RadioTransmitKHz + 3.0, txPlus3.RadioTransmitKHz, 3);
    }
}
