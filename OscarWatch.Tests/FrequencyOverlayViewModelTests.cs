using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public class FrequencyOverlayViewModelTests
{
    [Fact]
    public void Mode_switch_persists_offsets_for_new_mode_not_previous_spinner_values()
    {
        var settings = new TestSettingsService();
        settings.Current.FrequencySelections["RS-44"] = new SatelliteFrequencySelection
        {
            ModeType = "SSB Transponder",
            ModeIndex = 0,
            RememberOffsets = true
        };
        settings.Current.FrequencySelections["RS-44"].SetOffsetsForMode("SSB Transponder", 0, 4.025);
        settings.Current.FrequencySelections["RS-44"].SetOffsetsForMode("FT4", 0, 0);

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
                        UplinkKHz = 145_937.61,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    },
                    new SatelliteTransponderMode
                    {
                        Type = "FT4",
                        DownlinkKHz = 435_611,
                        UplinkKHz = 145_993.61,
                        DownlinkMode = "DATA-USB",
                        UplinkMode = "DATA-LSB",
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
            Subpoint = new GeoCoordinate(57, 18),
            LookAngles = new LookAngles(180, 25, 800, 2.5)
        });

        Assert.Equal("SSB Transponder", vm.SelectedMode?.Type);
        Assert.InRange(vm.ReceiveOffsetKHz, 4.024, 4.026);

        vm.SelectedMode = vm.AvailableModes.First(m => m.Type == "FT4");

        Assert.InRange(vm.ReceiveOffsetKHz, -0.001, 0.001);
        var stored = settings.Current.FrequencySelections["RS-44"];
        Assert.InRange(stored.ModeOffsets["FT4"].ReceiveOffsetKHz, -0.001, 0.001);
        Assert.InRange(stored.ModeOffsets["SSB Transponder"].ReceiveOffsetKHz, 4.024, 4.026);
    }

    [Fact]
    public void Doppler_strategy_persists_per_mode()
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
                        UplinkKHz = 145_937.61,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    },
                    new SatelliteTransponderMode
                    {
                        Type = "FT4",
                        DownlinkKHz = 435_611,
                        UplinkKHz = 145_993.61,
                        DownlinkMode = "DATA-USB",
                        UplinkMode = "DATA-LSB",
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
            Subpoint = new GeoCoordinate(57, 18),
            LookAngles = new LookAngles(180, 25, 800, 2.5)
        });

        Assert.True(vm.ShowDopplerStrategyRow);
        Assert.True(vm.IsFullDopplerSelected);

        vm.SetDopplerStrategy(DopplerStrategy.DownlinkOnly);
        Assert.True(vm.IsTxFixedDopplerSelected);

        vm.SelectedMode = vm.AvailableModes.First(m => m.Type == "FT4");
        Assert.True(vm.IsFullDopplerSelected);

        vm.SelectedMode = vm.AvailableModes.First(m => m.Type == "SSB Transponder");
        Assert.True(vm.IsTxFixedDopplerSelected);

        var stored = settings.Current.FrequencySelections["RS-44"];
        Assert.Equal(DopplerStrategy.DownlinkOnly, stored.GetDopplerStrategyForMode("SSB Transponder"));
        Assert.Equal(DopplerStrategy.Full, stored.GetDopplerStrategyForMode("FT4"));
    }

    [Fact]
    public void EnsureOverlayWithinHost_clamps_position_using_measured_overlay_size()
    {
        var settings = new TestSettingsService();
        settings.Current.FrequencyOverlayX = 500;
        settings.Current.FrequencyOverlayY = 900;
        var database = new TestSatelliteDatabaseService([]);
        var vm = new FrequencyOverlayViewModel(settings, database, LocalizationService.Instance);

        vm.EnsureOverlayWithinHost(800, 600, 400, 300);

        Assert.InRange(vm.OverlayX, 8, 800 - 400 - 8);
        Assert.InRange(vm.OverlayY, 8, 600 - 300 - 8);
        Assert.Equal(392, vm.OverlayX, precision: 0);
        Assert.Equal(292, vm.OverlayY, precision: 0);
    }

    [Fact]
    public void ToggleCollapse_persists_and_updates_compact_summary()
    {
        var settings = new TestSettingsService();
        var database = new TestSatelliteDatabaseService(
        [
            new SatelliteRadioEntry
            {
                Name = "JO-97",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 435_475,
                        UplinkKHz = 145_920,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    }
                ]
            }
        ]);

        var vm = new FrequencyOverlayViewModel(settings, database, LocalizationService.Instance);
        Assert.False(vm.IsCollapsed);

        vm.Update(new SatelliteTrackState
        {
            Name = "JO-97",
            NoradId = "1",
            Subpoint = new GeoCoordinate(52, -4),
            LookAngles = new LookAngles(180, 25, 800, 0)
        });

        Assert.Contains("JO-97", vm.CollapsedSummaryText, StringComparison.Ordinal);
        Assert.Contains("SSB Transponder", vm.CollapsedSummaryText, StringComparison.Ordinal);
        Assert.Contains("/", vm.CollapsedSummaryText, StringComparison.Ordinal);

        vm.ToggleCollapseCommand.Execute(null);
        Assert.True(vm.IsCollapsed);
        Assert.True(settings.Current.FrequencyOverlayCollapsed);
        Assert.Equal("▶", vm.CollapseToggleGlyph);

        vm.ToggleCollapseCommand.Execute(null);
        Assert.False(vm.IsCollapsed);
        Assert.False(settings.Current.FrequencyOverlayCollapsed);
        Assert.Equal("▼", vm.CollapseToggleGlyph);
    }

    [Fact]
    public void Cw_uplink_toggle_persists_per_mode_and_updates_rig_context()
    {
        var settings = new TestSettingsService();
        var database = new TestSatelliteDatabaseService(
        [
            new SatelliteRadioEntry
            {
                Name = "JO-97",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 435_475,
                        UplinkKHz = 145_920,
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
            Name = "JO-97",
            NoradId = "1",
            Subpoint = new GeoCoordinate(52, -4),
            LookAngles = new LookAngles(180, 25, 800, 0)
        });

        Assert.True(vm.ShowOperatingStyleRow);
        Assert.False(vm.IsCwUplink);

        settings.Current.Rig = new RigSettings { CwKeepSidebandDownlink = true };

        vm.SetCwUplink(true);
        Assert.True(vm.IsCwUplink);
        Assert.Contains("· CW", vm.CollapsedSummaryText, StringComparison.Ordinal);
        Assert.Contains("USB/LSB", vm.OperatingStyleHint, StringComparison.OrdinalIgnoreCase);

        var ctx = vm.TryBuildRigTrackingContext(new SatelliteTrackState
        {
            Name = "JO-97",
            NoradId = "1",
            Subpoint = new GeoCoordinate(52, -4),
            LookAngles = new LookAngles(180, 25, 800, 0)
        });
        Assert.NotNull(ctx);
        Assert.Equal("CW", ctx.EffectiveUplinkMode);
        Assert.Equal("USB", ctx.EffectiveDownlinkMode);

        Assert.True(settings.Current.FrequencySelections["JO-97"].GetCwUplinkForMode("SSB Transponder"));
    }

    [Fact]
    public void Cw_operating_style_uses_separate_stored_receive_offset()
    {
        var settings = new TestSettingsService();
        settings.Current.FrequencySelections["JO-97"] = new SatelliteFrequencySelection
        {
            ModeType = "SSB Transponder",
            ModeIndex = 0
        };
        settings.Current.FrequencySelections["JO-97"].SetOffsetsForMode("SSB Transponder", 0, 2.0);
        settings.Current.FrequencySelections["JO-97"].CwReceiveOffsetKHzByMode["SSB Transponder"] = -1.5;

        var database = new TestSatelliteDatabaseService(
        [
            new SatelliteRadioEntry
            {
                Name = "JO-97",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 435_475,
                        UplinkKHz = 145_920,
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
            Name = "JO-97",
            NoradId = "1",
            Subpoint = new GeoCoordinate(52, -4),
            LookAngles = new LookAngles(180, 25, 800, 0)
        });

        Assert.InRange(vm.ReceiveOffsetKHz, 1.999, 2.001);

        vm.SetCwUplink(true);
        Assert.InRange(vm.ReceiveOffsetKHz, -1.501, -1.499);

        vm.SetCwUplink(false);
        Assert.InRange(vm.ReceiveOffsetKHz, 1.999, 2.001);
    }

    [Fact]
    public void Store_offset_in_cw_mode_persists_cw_receive_offset_without_changing_voice()
    {
        var settings = new TestSettingsService();
        settings.Current.FrequencySelections["JO-97"] = new SatelliteFrequencySelection
        {
            ModeType = "SSB Transponder",
            ModeIndex = 0
        };
        settings.Current.FrequencySelections["JO-97"].SetOffsetsForMode("SSB Transponder", 0, 2.0);

        var database = new TestSatelliteDatabaseService(
        [
            new SatelliteRadioEntry
            {
                Name = "JO-97",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 435_475,
                        UplinkKHz = 145_920,
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
            Name = "JO-97",
            NoradId = "1",
            Subpoint = new GeoCoordinate(52, -4),
            LookAngles = new LookAngles(180, 25, 800, 0)
        });

        vm.SetCwUplink(true);
        vm.AdjustReceiveOffsetHz(-500);
        vm.StoreOffsetCommand.Execute(null);

        var stored = settings.Current.FrequencySelections["JO-97"];
        Assert.InRange(stored.CwReceiveOffsetKHzByMode["SSB Transponder"], 1.499, 1.501);
        Assert.InRange(stored.ModeOffsets["SSB Transponder"].ReceiveOffsetKHz, 1.999, 2.001);
        Assert.Contains("CW", vm.OffsetAppliedHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Offset_adjustment_does_not_persist_until_store()
    {
        var settings = new TestSettingsService();
        settings.Current.FrequencySelections["RS-44"] = new SatelliteFrequencySelection
        {
            ModeType = "SSB Transponder",
            ModeIndex = 0
        };
        settings.Current.FrequencySelections["RS-44"].SetOffsetsForMode("SSB Transponder", 0, 1.0);

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
                        UplinkKHz = 145_937.61,
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
            Subpoint = new GeoCoordinate(57, 18),
            LookAngles = new LookAngles(180, 25, 800, 2.5)
        });

        vm.AdjustReceiveOffsetHz(500);
        Assert.InRange(vm.ReceiveOffsetKHz, 1.499, 1.501);
        Assert.InRange(
            settings.Current.FrequencySelections["RS-44"].ModeOffsets["SSB Transponder"].ReceiveOffsetKHz,
            0.999,
            1.001);

        vm.StoreOffsetCommand.Execute(null);
        Assert.InRange(
            settings.Current.FrequencySelections["RS-44"].ModeOffsets["SSB Transponder"].ReceiveOffsetKHz,
            1.499,
            1.501);
    }

    [Fact]
    public void Cat_lead_radio_row_differs_from_snapshot_when_lead_rate_differs()
    {
        var settings = new TestSettingsService();
        settings.Current.Rig.CatDelayMs = 100;

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
                        UplinkKHz = 145_937.61,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    }
                ]
            }
        ]);

        var state = new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0, 400),
            LookAngles = new LookAngles(180, 30, 800, -1.0)
        };

        settings.Current.Rig.DopplerCatLeadEnabled = false;
        var withoutLead = new FrequencyOverlayViewModel(settings, database, LocalizationService.Instance);
        withoutLead.Update(state);

        settings.Current.Rig.DopplerCatLeadEnabled = true;
        var withLead = new FrequencyOverlayViewModel(
            settings,
            database,
            LocalizationService.Instance,
            new LeadRatePropagator(3.5));
        withLead.Update(state);

        Assert.NotEqual(withoutLead.RadioReceiveText, withLead.RadioReceiveText);
        Assert.Equal(withoutLead.SatelliteReceiveText, withLead.SatelliteReceiveText);
    }

    private sealed class LeadRatePropagator(double leadRate) : IOrbitPropagator
    {
        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 400);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(0, 0, 0);
        public bool HasSatellite(string noradId) => true;
        public IReadOnlyCollection<string> LoadedNoradIds => ["99999"];

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
            new(180, 30, 800, leadRate);
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
