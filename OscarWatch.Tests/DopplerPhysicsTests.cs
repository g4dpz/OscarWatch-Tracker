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
    public void Nor_mode_matches_golden_rx_tx_physics(DopplerFixture row)
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
        var corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 0);

        var expectedRx = row.RxHz / 1000.0;
        var expectedTx = row.TxHz / 1000.0;
        Assert.InRange(corrected.RadioReceiveKHz, expectedRx - 0.05, expectedRx + 0.05);
        Assert.InRange(corrected.RadioTransmitKHz, expectedTx - 0.05, expectedTx + 0.05);
    }

    [Fact]
    public void Rev_uses_same_doppler_math_as_nor()
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
            0);

        Assert.Equal(nor.RadioReceiveKHz, rev.RadioReceiveKHz, 3);
        Assert.Equal(nor.RadioTransmitKHz, rev.RadioTransmitKHz, 3);
    }

    [Fact]
    public void Rx_offset_applies_to_downlink_before_doppler()
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_667,
            UplinkKHz = 145_937,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var baseline = DopplerFrequencyCalculator.Compute(mode, 0, 0);
        var rxPlus2 = DopplerFrequencyCalculator.Compute(mode, 0, 2.5);

        Assert.True(rxPlus2.RadioReceiveKHz > baseline.RadioReceiveKHz);
        Assert.InRange(rxPlus2.RadioReceiveKHz - baseline.RadioReceiveKHz, 2.4, 2.6);
        Assert.Equal(baseline.RadioTransmitKHz, rxPlus2.RadioTransmitKHz, 3);
    }

    [Fact]
    public void Rev_passband_trim_moves_uplink_opposite_downlink()
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var baseline = DopplerFrequencyCalculator.Compute(mode, 0, 0);
        var trimmed = DopplerFrequencyCalculator.Compute(mode, 0, 0, passbandDownlinkAdjustKHz: 2.5, passbandUplinkAdjustKHz: -2.5);

        Assert.InRange(trimmed.RadioReceiveKHz - baseline.RadioReceiveKHz, 2.4, 2.6);
        Assert.InRange(trimmed.RadioTransmitKHz - baseline.RadioTransmitKHz, -2.6, -2.4);
    }

    [Fact]
    public void Sat_row_reflects_rx_offset_and_passband_trim()
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435.667,
            UplinkKHz = 145.937,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var corrected = DopplerFrequencyCalculator.Compute(
            mode,
            0,
            receiveOffsetKHz: -1.1,
            passbandDownlinkAdjustKHz: 0.5,
            passbandUplinkAdjustKHz: -0.3);

        Assert.InRange(corrected.SatelliteReceiveKHz, 435.667 - 1.1 + 0.5 - 0.001, 435.667 - 1.1 + 0.5 + 0.001);
        Assert.InRange(corrected.SatelliteTransmitKHz, 145.937 - 0.3 - 0.001, 145.937 - 0.3 + 0.001);

        var catalogOnly = DopplerFrequencyCalculator.Compute(mode, 2.5, 0);
        Assert.Equal(mode.DownlinkKHz, catalogOnly.SatelliteReceiveKHz, 3);
        Assert.NotEqual(catalogOnly.SatelliteReceiveKHz, corrected.SatelliteReceiveKHz);
    }

    [Fact]
    public void Nor_passband_trim_moves_both_legs_together()
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_667,
            UplinkKHz = 145_937,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "NOR"
        };

        var baseline = DopplerFrequencyCalculator.Compute(mode, 0, 0);
        var trimmed = DopplerFrequencyCalculator.Compute(mode, 0, 0, passbandDownlinkAdjustKHz: 2.0, passbandUplinkAdjustKHz: 2.0);

        Assert.InRange(trimmed.RadioReceiveKHz - baseline.RadioReceiveKHz, 1.9, 2.1);
        Assert.InRange(trimmed.RadioTransmitKHz - baseline.RadioTransmitKHz, 1.9, 2.1);
    }

    [Fact]
    public void Downlink_only_leaves_uplink_unchanged_by_doppler()
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_667,
            UplinkKHz = 145_937,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var full = DopplerFrequencyCalculator.Compute(mode, 2.5, 0, strategy: DopplerStrategy.Full);
        var txFixed = DopplerFrequencyCalculator.Compute(mode, 2.5, 0, strategy: DopplerStrategy.DownlinkOnly);

        Assert.Equal(full.RadioReceiveKHz, txFixed.RadioReceiveKHz, 3);
        Assert.Equal(mode.UplinkKHz, txFixed.RadioTransmitKHz, 3);
        Assert.NotEqual(full.RadioTransmitKHz, txFixed.RadioTransmitKHz);
    }

    [Fact]
    public void Uplink_only_leaves_downlink_unchanged_by_doppler()
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_667,
            UplinkKHz = 145_937,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var full = DopplerFrequencyCalculator.Compute(mode, 2.5, 0, strategy: DopplerStrategy.Full);
        var rxFixed = DopplerFrequencyCalculator.Compute(mode, 2.5, 0, strategy: DopplerStrategy.UplinkOnly);

        Assert.Equal(mode.DownlinkKHz, rxFixed.RadioReceiveKHz, 3);
        Assert.Equal(full.RadioTransmitKHz, rxFixed.RadioTransmitKHz, 3);
        Assert.NotEqual(full.RadioReceiveKHz, rxFixed.RadioReceiveKHz);
    }

    [Fact]
    public void Rx_offset_is_preserved_across_range_rate_changes()
    {
        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        const double rxOffset = 5.2;
        foreach (var rangeRate in new[] { 0.0, 2.5, 4.2, -1.5 })
        {
            var baseline = DopplerFrequencyCalculator.Compute(mode, rangeRate, 0);
            var offset = DopplerFrequencyCalculator.Compute(mode, rangeRate, rxOffset);
            Assert.InRange(offset.RadioReceiveKHz - baseline.RadioReceiveKHz, 5.1, 5.3);
            Assert.Equal(baseline.RadioTransmitKHz, offset.RadioTransmitKHz, 3);
        }
    }

}
