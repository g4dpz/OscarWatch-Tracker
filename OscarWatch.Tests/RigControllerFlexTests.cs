using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public class RigControllerFlexTests
{
    [Fact]
    public void Flex_cross_band_pass_enables_fdx_and_writes_rx_tx_frequencies()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();
        using var harness = CreateHarness(stub);

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 145_900,
            UplinkKHz = 435_800,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            CtcssHz = 67.0
        };

        PublishAndWait(
            harness,
            mode,
            DopplerFrequencyCalculator.Compute(mode, 0, 0),
            selectedCtcssHz: 67.0,
            ready: () => stub.FullDuplexEnabled);

        Assert.True(harness.Driver!.IsConnected);
        Assert.True(harness.Driver.IsSatelliteModeActive);
        Assert.True(stub.FullDuplexEnabled);

        var status = harness.Controller.GetStatus();
        Assert.True(status.IsTracking);
        Assert.NotNull(status.LastReceiveHz);
        Assert.NotNull(status.LastTransmitHz);

        Assert.Contains(stub.Slices.Values, s => s.FrequencyHz is >= 145_000_000 and <= 146_000_000);
        Assert.Contains(stub.Slices.Values, s => s.FrequencyHz is >= 435_000_000 and <= 436_000_000);
        Assert.Contains(
            stub.Slices.Values,
            s => s.Tx && s.ToneMode.Equals("ctcss_tx", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Flex_cross_band_pass_applies_configured_antenna_ports()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();
        using var harness = CreateHarness(stub);

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 435_800,
            UplinkKHz = 145_900,
            DownlinkMode = "FM",
            UplinkMode = "FM"
        };

        PublishAndWait(
            harness,
            mode,
            DopplerFrequencyCalculator.Compute(mode, 0, 0),
            settings: new RigSettings
            {
                Enabled = true,
                Type = RigType.FlexSmartSdr,
                NetworkHost = "127.0.0.1",
                NetworkPort = harness.Stub.Port,
                DopplerThresholdFmHz = 200,
                CatDelayMs = 50,
                FlexVhfRxAnt = "RX_B",
                FlexUhfRxAnt = "RX_A",
                FlexVhfTxAnt = "XVTR",
                FlexUhfTxAnt = "ANT1"
            },
            ready: () => stub.FullDuplexEnabled);

        var rxSlice = stub.Slices[harness.Driver!.RxSliceIndex];
        var txSlice = stub.Slices[harness.Driver.TxSliceIndex];
        Assert.Equal("RX_A", rxSlice.RxAnt);
        Assert.Equal("XVTR", txSlice.TxAnt);
    }

    [Fact]
    public void Flex_linear_manual_rx_tune_is_not_snapped_back()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();
        using var harness = CreateHarness(stub);
        var mode = new SatelliteTransponderMode
        {
            Type = "Linear",
            DownlinkKHz = 145_900,
            UplinkKHz = 435_800,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        PublishAndWait(
            harness,
            mode,
            DopplerFrequencyCalculator.Compute(mode, 0, 0),
            ready: () => harness.Controller.GetStatus().IsTracking
                && harness.Driver?.IsSatelliteModeActive == true);

        var tunedHz = 145_905_000L;
        stub.SetSliceFrequencyFromOperator(harness.Driver!.RxSliceIndex, tunedHz);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var actual = stub.Slices[harness.Driver.RxSliceIndex].FrequencyHz;
            if (Math.Abs(actual - tunedHz) <= 50
                && harness.Controller.GetStatus().LastReceiveHz is { } displayed
                && Math.Abs(displayed - tunedHz) <= 50)
            {
                Thread.Sleep(300);
                break;
            }

            Thread.Sleep(20);
        }

        Assert.InRange(
            stub.Slices[harness.Driver.RxSliceIndex].FrequencyHz,
            tunedHz - 50,
            tunedHz + 50);
        Assert.InRange(
            harness.Controller.GetStatus().LastReceiveHz!.Value,
            tunedHz - 50,
            tunedHz + 50);
    }

    [Fact]
    public void Flex_fdx_failure_is_visible_in_rig_status_and_stops_tracking()
    {
        using var stub = new FlexSmartSdrStubServer(rejectFullDuplex: true);
        stub.WaitUntilReady();
        using var harness = CreateHarness(stub);

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 145_900,
            UplinkKHz = 435_800,
            DownlinkMode = "FM",
            UplinkMode = "FM"
        };

        PublishAndWait(
            harness,
            mode,
            DopplerFrequencyCalculator.Compute(mode, 0, 0),
            ready: () => harness.Controller.GetStatus().StatusKind == RigStatusKind.FlexControlFailed);

        var status = harness.Controller.GetStatus();
        Assert.True(status.IsConnected);
        Assert.False(status.IsTracking);
        Assert.Equal(RigStatusKind.FlexControlFailed, status.StatusKind);
        Assert.Contains("full duplex", status.StatusDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Flex_same_band_packet_enables_fdx_with_two_slices_not_classic_split()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();
        using var harness = CreateHarness(stub);

        var mode = new SatelliteTransponderMode
        {
            Type = "Packet",
            DownlinkKHz = 145_825,
            UplinkKHz = 145_825,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR"
        };

        Assert.True(RigSatModeHelper.IsSameBandSimplex(mode.DownlinkKHz, mode.UplinkKHz));
        Assert.False(RigSatModeHelper.UseMainSubLayout(mode.DownlinkKHz, mode.UplinkKHz));

        PublishAndWait(
            harness,
            mode,
            DopplerFrequencyCalculator.Compute(mode, 2.5, 0),
            ready: () => stub.FullDuplexEnabled && harness.Driver?.IsSatelliteModeActive == true);

        Assert.True(stub.FullDuplexEnabled);
        Assert.True(harness.Driver!.IsSatelliteModeActive);

        // Same-band duplex still uses two slices (Main RX / Sub TX), not ICOM-style VFO A/B split.
        var vhfSlices = stub.Slices.Values
            .Where(s => s.FrequencyHz is >= 145_000_000 and <= 146_000_000)
            .ToList();
        Assert.True(vhfSlices.Count >= 2, $"Expected two VHF slices, got {vhfSlices.Count}");
        Assert.Contains(stub.Slices.Values, s => s.Tx);

        var status = harness.Controller.GetStatus();
        Assert.True(status.IsTracking);
        Assert.NotNull(status.LastReceiveHz);
        Assert.NotNull(status.LastTransmitHz);
        Assert.InRange(status.LastReceiveHz!.Value, 145_000_000, 146_000_000);
        Assert.InRange(status.LastTransmitHz!.Value, 145_000_000, 146_000_000);
    }

    [Fact]
    public void Flex_beacon_only_disables_fdx_and_tracks_downlink_only()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();
        using var harness = CreateHarness(stub);

        var mode = new SatelliteTransponderMode
        {
            Type = "SSTV (UHF)",
            DownlinkKHz = 437_550,
            UplinkKHz = 0,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR"
        };

        Assert.True(mode.IsBeaconOnly);

        PublishAndWait(
            harness,
            mode,
            DopplerFrequencyCalculator.Compute(mode, 0, 20),
            ready: () => harness.Controller.GetStatus().IsTracking
                && harness.Driver?.IsConnected == true
                && !stub.FullDuplexEnabled);

        Assert.False(stub.FullDuplexEnabled);
        Assert.False(harness.Driver!.IsSatelliteModeActive);

        var status = harness.Controller.GetStatus();
        Assert.True(status.IsTracking);
        Assert.NotNull(status.LastReceiveHz);
        Assert.InRange(status.LastReceiveHz!.Value, 437_000_000, 438_000_000);
        Assert.Null(status.LastTransmitHz);

        Assert.Contains(stub.Slices.Values, s => s.FrequencyHz is >= 437_000_000 and <= 438_000_000);
        Assert.DoesNotContain(
            stub.Slices.Values,
            s => s.ToneMode.Equals("ctcss_tx", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Flex_beacon_after_same_band_pass_turns_fdx_off()
    {
        using var stub = new FlexSmartSdrStubServer();
        stub.WaitUntilReady();
        using var harness = CreateHarness(stub);

        var packet = new SatelliteTransponderMode
        {
            Type = "Packet",
            DownlinkKHz = 145_825,
            UplinkKHz = 145_825,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR"
        };

        PublishAndWait(
            harness,
            packet,
            DopplerFrequencyCalculator.Compute(packet, 0, 20),
            ready: () => stub.FullDuplexEnabled);
        Assert.True(stub.FullDuplexEnabled);

        var beacon = new SatelliteTransponderMode
        {
            Type = "SSTV (UHF)",
            DownlinkKHz = 437_550,
            UplinkKHz = 0,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR"
        };

        PublishAndWait(
            harness,
            beacon,
            DopplerFrequencyCalculator.Compute(beacon, 0, 20),
            ready: () => !stub.FullDuplexEnabled && harness.Driver?.IsSatelliteModeActive == false);

        Assert.False(stub.FullDuplexEnabled);
        Assert.False(harness.Driver!.IsSatelliteModeActive);

        var status = harness.Controller.GetStatus();
        Assert.True(status.IsTracking);
        Assert.NotNull(status.LastReceiveHz);
        Assert.Null(status.LastTransmitHz);
    }

    private static FlexHarness CreateHarness(FlexSmartSdrStubServer stub)
    {
        FlexRadioDriver? driver = null;
        var controller = new RigController(settings =>
        {
            driver = new FlexRadioDriver(settings.NetworkHost, settings.NetworkPort, settings.CatDelayMs);
            return driver;
        });

        return new FlexHarness(controller, stub, () => driver);
    }

    private static void PublishAndWait(
        FlexHarness harness,
        SatelliteTransponderMode mode,
        CorrectedFrequencies corrected,
        Func<bool> ready,
        double? selectedCtcssHz = null,
        RigSettings? settings = null)
    {
        settings ??= new RigSettings
        {
            Enabled = true,
            Type = RigType.FlexSmartSdr,
            NetworkHost = "127.0.0.1",
            NetworkPort = harness.Stub.Port,
            DopplerThresholdFmHz = 200,
            CatDelayMs = 50
        };

        settings.Enabled = true;
        settings.Type = RigType.FlexSmartSdr;
        settings.NetworkHost = "127.0.0.1";
        settings.NetworkPort = harness.Stub.Port;

        var ctx = new RigTrackingContext
        {
            TrackState = new SatelliteTrackState
            {
                Name = mode.Type,
                NoradId = "25544",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 25, 900, 1.5)
            },
            Mode = mode,
            Corrected = corrected,
            SelectedCtcssHz = selectedCtcssHz
        };

        harness.Controller.PublishContext(settings, ctx);
        harness.Controller.DrainCommandQueueForTests();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var status = harness.Controller.GetStatus();
            if (status.IsConnected && ready())
                return;
            Thread.Sleep(50);
        }

        var finalStatus = harness.Controller.GetStatus();
        throw new TimeoutException(
            $"Flex controller did not reach the expected state. Status={finalStatus.StatusKind}, detail={finalStatus.StatusDetail}");
    }

    private sealed class FlexHarness : IDisposable
    {
        private readonly Func<FlexRadioDriver?> _driverAccessor;

        public FlexHarness(RigController controller, FlexSmartSdrStubServer stub, Func<FlexRadioDriver?> driverAccessor)
        {
            Controller = controller;
            Stub = stub;
            _driverAccessor = driverAccessor;
        }

        public RigController Controller { get; }
        public FlexSmartSdrStubServer Stub { get; }
        public FlexRadioDriver? Driver => _driverAccessor();

        public void Dispose() => Controller.Dispose();
    }
}
