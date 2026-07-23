using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.ViewModels;
using static OscarWatch.Localization.LocalizationCulture;

namespace OscarWatch.Tests;

public class SatellitePickerViewModelTests
{
    [Fact]
    public void Load_checks_satellites_that_are_enabled_only_via_fuzzy_name_match()
    {
        using var _ = TestUiCulture.Apply(DefaultLanguage);

        // Classic AMSAT alias: settings store "SO-50", catalog uses the long TLE name.
        var so50 = Entry("SAUDISAT 1C (SO-50)", "27607");
        var iss = Entry("ISS", "25544");
        var settings = new TestSettingsService();
        settings.Current.EnabledSatelliteNames = ["SO-50"];
        var tle = new StubTleService([so50, iss]);

        Assert.True(SatelliteCatalogMatching.IsEnabled(so50, new HashSet<string>(settings.Current.EnabledSatelliteNames, StringComparer.OrdinalIgnoreCase)));
        Assert.Equal([so50], tle.GetEnabledSatellites(settings.Current));

        var vm = new SatellitePickerViewModel(settings, tle, LocalizationService.Instance);

        var so50Item = Assert.Single(vm.Satellites, s => s.NoradId == "27607");
        Assert.True(so50Item.IsEnabled);
        Assert.False(Assert.Single(vm.Satellites, s => s.NoradId == "25544").IsEnabled);
    }

    [Fact]
    public void Load_checks_short_catalog_name_when_enabled_list_has_longer_containing_name()
    {
        using var _ = TestUiCulture.Apply(DefaultLanguage);

        // After a TLE source change, settings may still hold a longer historical name.
        var isat = Entry("ISAT", "43879");
        var origami = Entry("OrigamiSat 2", "68795");
        var settings = new TestSettingsService();
        settings.Current.EnabledSatelliteNames = ["ISAT (CUBE)", "OrigamiSat 2"];
        var tle = new StubTleService([isat, origami]);

        Assert.Equal(2, tle.GetEnabledSatellites(settings.Current).Count);

        var vm = new SatellitePickerViewModel(settings, tle, LocalizationService.Instance);

        Assert.True(Assert.Single(vm.Satellites, s => s.Name == "ISAT").IsEnabled);
        Assert.True(Assert.Single(vm.Satellites, s => s.Name == "OrigamiSat 2").IsEnabled);
    }

    [Fact]
    public void OrigamiSat_2_alone_does_not_show_ISAT_as_enabled()
    {
        using var _ = TestUiCulture.Apply(DefaultLanguage);

        // Reporter case: only OrigamiSat 2 ticked, but ISAT appeared on the map because
        // "OrigamiSat" contains the mid-token letters iSat.
        var isat = Entry("ISAT", "43879");
        var origami = Entry("OrigamiSat 2", "68795");
        var settings = new TestSettingsService();
        settings.Current.EnabledSatelliteNames = ["OrigamiSat 2"];
        var tle = new StubTleService([isat, origami]);

        Assert.Equal([origami], tle.GetEnabledSatellites(settings.Current));

        var vm = new SatellitePickerViewModel(settings, tle, LocalizationService.Instance);

        Assert.False(Assert.Single(vm.Satellites, s => s.Name == "ISAT").IsEnabled);
        Assert.True(Assert.Single(vm.Satellites, s => s.Name == "OrigamiSat 2").IsEnabled);
    }

    [Fact]
    public async Task Save_rewrites_fuzzy_enabled_names_to_current_catalog_names()
    {
        using var _ = TestUiCulture.Apply(DefaultLanguage);

        var so50 = Entry("SAUDISAT 1C (SO-50)", "27607");
        var settings = new TestSettingsService();
        settings.Current.EnabledSatelliteNames = ["SO-50"];
        var tle = new StubTleService([so50]);
        var vm = new SatellitePickerViewModel(settings, tle, LocalizationService.Instance);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["SAUDISAT 1C (SO-50)"], settings.Current.EnabledSatelliteNames);
    }

    [Fact]
    public async Task Save_unchecking_removes_fuzzy_enabled_satellite()
    {
        using var _ = TestUiCulture.Apply(DefaultLanguage);

        var isat = Entry("ISAT", "43879");
        var origami = Entry("OrigamiSat 2", "68795");
        var settings = new TestSettingsService();
        settings.Current.EnabledSatelliteNames = ["ISAT (CUBE)", "OrigamiSat 2"];
        var tle = new StubTleService([isat, origami]);
        var vm = new SatellitePickerViewModel(settings, tle, LocalizationService.Instance);

        Assert.Single(vm.Satellites, s => s.Name == "ISAT").IsEnabled = false;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["OrigamiSat 2"], settings.Current.EnabledSatelliteNames);
        Assert.Equal([origami], tle.GetEnabledSatellites(settings.Current));
    }

    private static SatelliteCatalogEntry Entry(string name, string noradId) => new()
    {
        Name = name,
        NoradId = noradId,
        Line1 = $"1 {noradId}U 00000A   00001.00000000  .00000000  00000-0  00000-0 0  0000",
        Line2 = $"2 {noradId}  00.0000 000.0000 0000000 000.0000 000.0000 15.00000000 00000",
        EpochUtc = DateTime.UtcNow
    };

    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "oscarwatch-picker-test-settings.json");
        public string SerializeCurrent() => "{}";
        public Task ReplaceAndSaveAsync(AppSettings imported, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Load() { }
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RequestSave() { }
        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SyncGridFromLatLon() { }
        public void SyncLatLonFromGrid() { }
        public void EnsureSavedStations() { }
        public void ApplyActiveStation() { }
        public void SyncActiveStationFromGroundStation() { }
    }

    private sealed class StubTleService(IReadOnlyList<SatelliteCatalogEntry> catalog) : ITleService
    {
        public IReadOnlyList<SatelliteCatalogEntry> Catalog => catalog;
        public DateTime? LastFetchedUtc => DateTime.UtcNow;
        public string CachePath => "";
        public TleCatalogLoadDiagnostics? LastLoadDiagnostics => null;
        public bool IsStale(int staleHours) => false;
        public Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateCatalog() { }
        public string ActiveSourceLabel => "Test";

        public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings)
        {
            var enabled = new HashSet<string>(settings.EnabledSatelliteNames, StringComparer.OrdinalIgnoreCase);
            return catalog.Where(s => SatelliteCatalogMatching.IsEnabled(s, enabled)).ToList();
        }
    }
}
