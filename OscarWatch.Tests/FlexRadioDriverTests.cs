using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public class FlexRadioDriverTests
{
    [Fact]
    public void Open_ConnectsAndCachesSlices()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();

        Assert.True(driver.IsConnected);
        Assert.Equal(RigType.FlexSmartSdr, driver.RigType);

        var rx = driver.ReadFrequencyHz(RigVfo.Main);
        var tx = driver.ReadFrequencyHz(RigVfo.Sub);
        Assert.Equal(145_900_000, rx);
        Assert.Equal(435_000_000, tx);
    }

    [Fact]
    public void SetSatelliteMode_EnablesFullDuplexAndTxSlice()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        Assert.True(driver.IsSatelliteModeActive);
        Assert.True(stub.FullDuplexEnabled);
        Assert.Contains(stub.Slices.Values, s => s.Tx);
    }

    [Fact]
    public void SetSatelliteMode_WhenFullDuplexRejected_ThrowsAndRemainsInactive()
    {
        using var stub = new FlexSmartSdrStubServer(rejectFullDuplex: true);
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();

        var ex = Assert.Throws<FlexSatelliteSetupException>(() => driver.SetSatelliteMode(true));

        Assert.Contains("full duplex", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(driver.IsSatelliteModeActive);
        Assert.False(stub.FullDuplexEnabled);
    }

    [Fact]
    public void SetSatelliteMode_WhenSecondSliceCannotBeCreated_ThrowsAndDisablesFullDuplex()
    {
        using var stub = new FlexSmartSdrStubServer(initialSliceCount: 1, rejectSliceCreate: true);
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();

        var ex = Assert.Throws<FlexSatelliteSetupException>(() => driver.SetSatelliteMode(true));

        Assert.Contains("separate RX and TX slices", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(driver.IsSatelliteModeActive);
        Assert.False(stub.FullDuplexEnabled);
    }

    [Fact]
    public void SetSatelliteMode_ConfirmsCreatedSliceFromStatusWhenResponseOmitsIndex()
    {
        using var stub = new FlexSmartSdrStubServer(initialSliceCount: 1, omitSliceCreateIndex: true);
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        Assert.True(driver.IsSatelliteModeActive);
        Assert.NotEqual(driver.RxSliceIndex, driver.TxSliceIndex);
        Assert.Equal(2, stub.Slices.Count);
        Assert.True(stub.Slices[driver.TxSliceIndex].Tx);
    }

    [Fact]
    public void SetSatelliteMode_WhenTxSliceRejected_ThrowsAndDisablesFullDuplex()
    {
        using var stub = new FlexSmartSdrStubServer(rejectTxSlice: true);
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();

        var ex = Assert.Throws<FlexSatelliteSetupException>(() => driver.SetSatelliteMode(true));

        Assert.Contains("TX slice", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(driver.IsSatelliteModeActive);
        Assert.False(stub.FullDuplexEnabled);
    }

    [Fact]
    public void SetFrequencyHz_TunesCurrentVfoSlice()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        driver.SelectVfo(RigVfo.Main);
        Assert.True(driver.SetFrequencyHz(145_800_000));
        Assert.Equal(145_800_000, stub.Slices[driver.RxSliceIndex].FrequencyHz);

        driver.SelectVfo(RigVfo.Sub);
        Assert.True(driver.SetFrequencyHz(435_100_000));
        Assert.Equal(435_100_000, stub.Slices[driver.TxSliceIndex].FrequencyHz);
    }

    [Fact]
    public void SetMode_AndTone_ApplyToSlices()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        driver.SelectVfo(RigVfo.Main);
        driver.SetMode("FM");
        Assert.Equal("FM", stub.Slices[driver.RxSliceIndex].Mode);

        driver.SelectVfo(RigVfo.Sub);
        driver.SetMode("FM");
        driver.SetToneHz(67.0, squelchTone: false);
        driver.SetToneOn(true);

        var tx = stub.Slices[driver.TxSliceIndex];
        Assert.Equal("FM", tx.Mode);
        Assert.Equal("ctcss_tx", tx.ToneMode);
        Assert.Equal(67.0, tx.ToneHz);
    }

    [Fact]
    public void SupportsVfoExchange_IsFalse()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();
        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port);
        Assert.False(driver.SupportsVfoExchange);
    }
}
