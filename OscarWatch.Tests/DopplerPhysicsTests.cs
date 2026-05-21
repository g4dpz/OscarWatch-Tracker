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
}
