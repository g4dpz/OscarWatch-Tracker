using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public class FrequencyOverlayRigContextTests
{
    [Fact]
    public void TryBuildRigTrackingContext_returns_null_when_overlay_not_synced_to_state()
    {
        var settings = new TestSettingsService();
        var database = new TestSatelliteDatabaseService(
        [
            new SatelliteRadioEntry
            {
                Name = "FO-29",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 435_850.45,
                        UplinkKHz = 145_952.65,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    }
                ]
            },
            new SatelliteRadioEntry
            {
                Name = "JO-97",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 145_865,
                        UplinkKHz = 435_110.1,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    }
                ]
            }
        ]);

        var vm = new FrequencyOverlayViewModel(settings, database, LocalizationService.Instance);
        vm.Update(new SatelliteTrackState
        {
            Name = "FO-29",
            NoradId = "11111",
            Subpoint = new GeoCoordinate(57, 18),
            LookAngles = new LookAngles(180, 25, 800, 2.5)
        });

        var jo97 = new SatelliteTrackState
        {
            Name = "JO-97",
            NoradId = "22222",
            Subpoint = new GeoCoordinate(57, 18),
            LookAngles = new LookAngles(180, 25, 800, 2.5)
        };

        Assert.Null(vm.TryBuildRigTrackingContext(jo97));

        vm.Update(jo97);
        Assert.NotNull(vm.TryBuildRigTrackingContext(jo97));
    }

    [Fact]
    public void TryBuildRigTrackingContext_includes_receive_offset_on_radio_and_sat_rows()
    {
        var settings = new TestSettingsService();
        var database = new TestSatelliteDatabaseService(
        [
            new SatelliteRadioEntry
            {
                Name = "RS-44",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 435_667,
                        UplinkKHz = 145_937,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    }
                ]
            }
        ]);

        var vm = new FrequencyOverlayViewModel(settings, database, LocalizationService.Instance);
        vm.Update(new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        });

        const double rxOffsetKHz = 4.025;
        vm.ReceiveOffsetKHz = rxOffsetKHz;

        var ctx = vm.TryBuildRigTrackingContext(new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        });

        Assert.NotNull(ctx);
        Assert.Equal(rxOffsetKHz, ctx.ReceiveOffsetKHz);

        var baseline = DopplerFrequencyCalculator.Compute(ctx.Mode, 0, 0);
        var withOffset = DopplerFrequencyCalculator.Compute(ctx.Mode, 0, rxOffsetKHz);

        Assert.InRange(ctx.Corrected.RadioReceiveKHz - baseline.RadioReceiveKHz, 4.0, 4.1);
        Assert.InRange(ctx.Corrected.SatelliteReceiveKHz - ctx.Mode.DownlinkKHz, 4.0, 4.1);
        Assert.Equal(withOffset.RadioReceiveKHz, ctx.Corrected.RadioReceiveKHz, 3);
        Assert.Equal(baseline.RadioTransmitKHz, ctx.Corrected.RadioTransmitKHz, 3);
    }

    [Fact]
    public void TryBuildRigTrackingContext_includes_transmit_offset_on_uplink()
    {
        var settings = new TestSettingsService();
        var database = new TestSatelliteDatabaseService(
        [
            new SatelliteRadioEntry
            {
                Name = "RS-44",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 435_667,
                        UplinkKHz = 145_937,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    }
                ]
            }
        ]);

        var vm = new FrequencyOverlayViewModel(settings, database, LocalizationService.Instance);
        vm.Update(new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        });

        const double txOffsetKHz = -1.5;
        vm.IsTransmitOffsetSelected = true;
        vm.TransmitOffsetKHz = txOffsetKHz;

        var ctx = vm.TryBuildRigTrackingContext(new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        });

        Assert.NotNull(ctx);
        Assert.Equal(txOffsetKHz, ctx.TransmitOffsetKHz);

        var baseline = DopplerFrequencyCalculator.Compute(ctx.Mode, 0, 0);
        Assert.Equal(baseline.RadioReceiveKHz, ctx.Corrected.RadioReceiveKHz, 3);
        Assert.InRange(ctx.Corrected.RadioTransmitKHz - baseline.RadioTransmitKHz, -1.6, -1.4);
    }

    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "oscarwatch-test-settings.json");
        public string SerializeCurrent() => "{}";
        public Task ReplaceAndSaveAsync(AppSettings imported, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Load() { }
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RequestSave() { }
        public void SyncGridFromLatLon() { }
        public void SyncLatLonFromGrid() { }
        public void EnsureSavedStations() { }
        public void ApplyActiveStation() { }
        public void SyncActiveStationFromGroundStation() { }
    }

    private sealed class TestSatelliteDatabaseService(IReadOnlyList<SatelliteRadioEntry> entries) : ISatelliteDatabaseService
    {
        public IReadOnlyList<SatelliteRadioEntry> Entries { get; } = entries;
        public string ActiveDatabasePath { get; } = "test";
        public bool IsUsingUserDatabase => false;

        public SatelliteRadioEntry? TryGetEntry(string satelliteName) =>
            Entries.FirstOrDefault(e => e.Name.Equals(satelliteName, StringComparison.OrdinalIgnoreCase));

        public void Reload() { }
    }
}
