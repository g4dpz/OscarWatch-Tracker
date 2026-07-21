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
    public void Open_WhenOptionalClientLabelIsRejected_StillConnects()
    {
        using var stub = new FlexSmartSdrStubServer(rejectClientProgram: true);
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();

        Assert.True(driver.IsConnected);
        Assert.Equal(145_900_000, driver.ReadFrequencyHz(RigVfo.Main));
        Assert.Equal(435_000_000, driver.ReadFrequencyHz(RigVfo.Sub));
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
    public void ReadFrequencyHz_DrainsUnsolicitedManualSliceStatus()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();
        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        stub.SetSliceFrequencyFromOperator(driver.RxSliceIndex, 145_850_000);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        long? observed = null;
        while (DateTime.UtcNow < deadline && observed != 145_850_000)
        {
            observed = driver.ReadFrequencyHz(RigVfo.Main);
            Thread.Sleep(10);
        }

        Assert.Equal(145_850_000, observed);
    }

    [Fact]
    public void SetFrequencyHz_RejectsStaleRxTuneWhenNewerStatusArrives()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();
        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);
        Assert.Equal(145_900_000, driver.ReadFrequencyHz(RigVfo.Main));

        stub.SetSliceFrequencyFromOperator(driver.RxSliceIndex, 145_850_000);
        Thread.Sleep(50);

        driver.SelectVfo(RigVfo.Main);
        Assert.False(driver.SetFrequencyHz(145_901_000));
        Assert.Equal(145_850_000, stub.Slices[driver.RxSliceIndex].FrequencyHz);
        Assert.Equal(145_850_000, driver.ReadFrequencyHz(RigVfo.Main));
    }

    [Fact]
    public void SetMode_AndTone_ApplyToSlices()
    {
        using var stub = new FlexSmartSdrStubServer(emitPartialSliceStatus: true);
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        driver.SelectVfo(RigVfo.Main);
        driver.SetMode("FMN");
        Assert.Equal("NFM", stub.Slices[driver.RxSliceIndex].Mode);

        driver.SelectVfo(RigVfo.Sub);
        driver.SetMode("FM");
        driver.SetToneHz(67.0, squelchTone: false);
        driver.SetToneOn(true);

        var tx = stub.Slices[driver.TxSliceIndex];
        Assert.Equal("FM", tx.Mode);
        Assert.Equal("ctcss_tx", tx.ToneMode);
        Assert.Equal(67.0, tx.ToneHz);
        Assert.Equal(145_900_000, driver.ReadFrequencyHz(RigVfo.Main));
        Assert.Equal(435_000_000, driver.ReadFrequencyHz(RigVfo.Sub));
    }

    [Fact]
    public void SetTone_WhenRadioDoesNotEchoOwnStatus_SendsModeAndValue()
    {
        using var stub = new FlexSmartSdrStubServer(suppressSliceSetStatus: true);
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);
        driver.SelectVfo(RigVfo.Sub);
        driver.SetMode("FM");
        driver.SetToneHz(74.4, squelchTone: false);
        driver.SetToneOn(true);

        var tx = stub.Slices[driver.TxSliceIndex];
        Assert.Equal("ctcss_tx", tx.ToneMode);
        Assert.Equal(74.4, tx.ToneHz);
        Assert.Equal(435_000_000, driver.ReadFrequencyHz(RigVfo.Sub));
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
