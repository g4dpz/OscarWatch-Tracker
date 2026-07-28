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
    public void SetSatelliteMode_RetriesWhenInitialSliceCreatesFailTransiently()
    {
        // Zero slices + first two create attempts rejected (one EnsureDuplex pass), then creates succeed.
        using var stub = new FlexSmartSdrStubServer(initialSliceCount: 0, rejectSliceCreateCount: 2);
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        Assert.True(driver.IsSatelliteModeActive);
        Assert.True(stub.FullDuplexEnabled);
        Assert.NotEqual(driver.RxSliceIndex, driver.TxSliceIndex);
        Assert.Equal(2, stub.Slices.Count);
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
    public void ApplyBandAntennaPorts_sets_rx_and_tx_ports_for_duplex_layout()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        var settings = new RigSettings
        {
            FlexVhfRxAnt = "RX_B",
            FlexUhfRxAnt = "RX_A",
            FlexVhfTxAnt = "XVTR",
            FlexUhfTxAnt = "ANT1"
        };

        stub.ClearCommandBodies();
        driver.ApplyBandAntennaPorts(settings, 435_300_000, 145_800_000);

        var rx = stub.Slices[driver.RxSliceIndex];
        var tx = stub.Slices[driver.TxSliceIndex];
        Assert.Equal("RX_A", rx.RxAnt);
        Assert.Equal("XVTR", tx.TxAnt);
        Assert.Contains(
            stub.CommandBodies,
            b => b.Equals("slice set 0 rxant=RX_A", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            stub.CommandBodies,
            b => b.Equals("slice set 1 txant=XVTR", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyBandAntennaPorts_with_empty_settings_sends_no_antenna_commands()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        driver.ApplyBandAntennaPorts(new RigSettings(), 435_300_000, 145_800_000);

        foreach (var slice in stub.Slices.Values)
        {
            Assert.Equal("", slice.RxAnt);
            Assert.Equal("", slice.TxAnt);
        }
    }

    [Fact]
    public void CenterBandPanadapters_centres_vhf_and_uhf_band_pans()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        stub.ClearCommandBodies();
        driver.CenterBandPanadapters(145_960_000, 435_148_000);

        Assert.Equal(145_960_000, stub.PanCentersHz["0x40000001"]);
        Assert.Equal(435_148_000, stub.PanCentersHz["0x40000000"]);
        Assert.Contains(
            stub.CommandBodies,
            b => b.Equals("display pan set 0x40000001 center=145.96 autocenter=0", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            stub.CommandBodies,
            b => b.Equals("display pan set 0x40000000 center=435.148 autocenter=0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CenterBandPanadapters_vu_layout_centres_uhf_and_vhf_band_pans()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        stub.ClearCommandBodies();
        driver.CenterBandPanadapters(435_863_000, 145_943_000);

        Assert.Equal(435_863_000, stub.PanCentersHz["0x40000000"]);
        Assert.Equal(145_943_000, stub.PanCentersHz["0x40000001"]);
        Assert.Contains(
            stub.CommandBodies,
            b => b.Equals("display pan set 0x40000000 center=435.863 autocenter=0", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            stub.CommandBodies,
            b => b.Equals("display pan set 0x40000001 center=145.943 autocenter=0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BindDuplexSlicesToBandPans_vu_layout_binds_rx_uhf_tx_vhf()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        driver.BindDuplexSlicesToBandPans(435_863_000, 145_943_000);

        Assert.Equal("0x40000000", stub.Slices[driver.RxSliceIndex].PanStreamId);
        Assert.Equal("0x40000001", stub.Slices[driver.TxSliceIndex].PanStreamId);
    }

    [Fact]
    public void Deferred_modes_set_usb_on_rx_and_lsb_on_tx()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        driver.BindDuplexSlicesToBandPans(145_950_000, 432_146_000);
        driver.SelectVfo(RigVfo.Main);
        driver.SetFrequencyHz(145_950_000);
        driver.SelectVfo(RigVfo.Sub);
        driver.SetFrequencyHz(432_146_000);
        driver.CenterBandPanadapters(145_950_000, 432_146_000);
        driver.SelectVfo(RigVfo.Main);
        driver.SetMode("USB");
        driver.SelectVfo(RigVfo.Sub);
        driver.SetMode("LSB");

        Assert.Equal("USB", stub.Slices[driver.RxSliceIndex].Mode);
        Assert.Equal("LSB", stub.Slices[driver.TxSliceIndex].Mode);
    }

    [Fact]
    public void EnsureDuplexPassFrequencies_retunes_when_rx_slice_wrong()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        driver.BindDuplexSlicesToBandPans(145_950_000, 432_146_000);
        driver.SelectVfo(RigVfo.Main);
        driver.SetFrequencyHz(145_950_000);
        driver.SelectVfo(RigVfo.Sub);
        driver.SetFrequencyHz(432_146_000);
        driver.CenterBandPanadapters(145_950_000, 432_146_000);

        driver.SelectVfo(RigVfo.Main);
        driver.SetFrequencyHz(145_867_000);
        Assert.Equal(145_867_000, stub.Slices[driver.RxSliceIndex].FrequencyHz);

        stub.ClearCommandBodies();
        driver.EnsureDuplexPassFrequencies(145_950_000, 432_146_000);

        Assert.Equal(145_950_000, stub.Slices[driver.RxSliceIndex].FrequencyHz);
        Assert.Equal(432_146_000, stub.Slices[driver.TxSliceIndex].FrequencyHz);
        Assert.Contains(
            stub.CommandBodies,
            b => b.StartsWith("slice tune ", StringComparison.OrdinalIgnoreCase)
                 && b.Contains(" 145.95", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureDuplexPassFrequencies_noop_when_slices_already_correct()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        driver.BindDuplexSlicesToBandPans(435_640_000, 145_965_000);
        driver.SelectVfo(RigVfo.Main);
        driver.SetFrequencyHz(435_640_000);
        driver.SelectVfo(RigVfo.Sub);
        driver.SetFrequencyHz(145_965_000);
        driver.CenterBandPanadapters(435_640_000, 145_965_000);

        stub.ClearCommandBodies();
        driver.EnsureDuplexPassFrequencies(435_640_000, 145_965_000);

        Assert.DoesNotContain(
            stub.CommandBodies,
            b => b.StartsWith("slice tune ", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            stub.CommandBodies,
            b => b.StartsWith("slice remove ", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(435_640_000, stub.Slices[driver.RxSliceIndex].FrequencyHz);
        Assert.Equal(145_965_000, stub.Slices[driver.TxSliceIndex].FrequencyHz);
    }

    [Fact]
    public void EnsureDuplexPassFrequencies_repairs_wrong_rx_mode()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();

        using var driver = new FlexRadioDriver("127.0.0.1", stub.Port, catDelayMs: 50);
        driver.Open();
        driver.SetSatelliteMode(true);

        driver.BindDuplexSlicesToBandPans(145_950_000, 432_146_000);
        driver.SelectVfo(RigVfo.Main);
        driver.SetFrequencyHz(145_950_000);
        driver.SelectVfo(RigVfo.Sub);
        driver.SetFrequencyHz(432_146_000);
        driver.CenterBandPanadapters(145_950_000, 432_146_000);
        driver.SelectVfo(RigVfo.Main);
        driver.SetMode("FM");
        driver.SelectVfo(RigVfo.Sub);
        driver.SetMode("LSB");

        stub.ClearCommandBodies();
        driver.EnsureDuplexPassFrequencies(145_950_000, 432_146_000, "USB", "LSB");

        Assert.Equal("USB", stub.Slices[driver.RxSliceIndex].Mode);
        Assert.Equal("LSB", stub.Slices[driver.TxSliceIndex].Mode);
        Assert.Contains(
            stub.CommandBodies,
            b => b.Contains("slice set ", StringComparison.OrdinalIgnoreCase)
                 && b.Contains(" mode=USB", StringComparison.OrdinalIgnoreCase));
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
