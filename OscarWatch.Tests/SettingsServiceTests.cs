using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void TryParse_round_trips_serialized_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oscarwatch-parse-{Guid.NewGuid():N}.json");
        var service = new SettingsService(path);
        service.Current.GroundStation.DisplayName = "Portable";
        service.Current.EnabledSatelliteNames = ["ISS", "SO-50"];
        service.Current.Rig.Enabled = true;

        var json = service.SerializeCurrent();

        Assert.True(SettingsService.TryParse(json, out var parsed, out var error));
        Assert.Null(error);
        Assert.Equal("Portable", parsed.GroundStation.DisplayName);
        Assert.Equal(["ISS", "SO-50"], parsed.EnabledSatelliteNames);
        Assert.True(parsed.Rig.Enabled);
    }

    [Fact]
    public async Task ReplaceAndSaveAsync_persists_imported_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oscarwatch-import-{Guid.NewGuid():N}.json");
        var service = new SettingsService(path);
        service.Current.GroundStation.DisplayName = "Before";

        var imported = new AppSettings
        {
            GroundStation = new GroundStation { DisplayName = "After", LatitudeDeg = 51.5, LongitudeDeg = -0.1 }
        };

        try
        {
            await service.ReplaceAndSaveAsync(imported);

            Assert.Equal("After", service.Current.GroundStation.DisplayName);
            var reloaded = new SettingsService(path);
            reloaded.Load();
            Assert.Equal("After", reloaded.Current.GroundStation.DisplayName);
        }
        finally
        {
            DeleteIfExists(path);
            DeleteIfExists(path + ".tmp");
            DeleteIfExists(path + ".bak");
        }
    }

    [Fact]
    public async Task SaveAsync_serializes_concurrent_writes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oscarwatch-settings-{Guid.NewGuid():N}.json");
        var service = new SettingsService(path);
        service.Current.GroundStation.DisplayName = "Home";

        try
        {
            var tasks = Enumerable.Range(0, 20)
                .Select(i =>
                {
                    service.Current.GroundStation.DisplayName = $"Station-{i}";
                    return service.SaveAsync();
                })
                .ToArray();

            await Task.WhenAll(tasks);

            Assert.True(File.Exists(path));
            var json = await File.ReadAllTextAsync(path);
            Assert.Contains("Station-19", json);
        }
        finally
        {
            DeleteIfExists(path);
            DeleteIfExists(path + ".tmp");
            DeleteIfExists(path + ".bak");
        }
    }

    [Fact]
    public void WriteAtomic_replaces_existing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oscarwatch-atomic-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{ \"old\": true }");
            SettingsService.WriteAtomic(path, "{ \"new\": true }");

            var json = File.ReadAllText(path);
            Assert.Contains("\"new\": true", json);
            Assert.DoesNotContain("\"old\": true", json);
        }
        finally
        {
            DeleteIfExists(path);
            DeleteIfExists(path + ".tmp");
            DeleteIfExists(path + ".bak");
        }
    }

    [Fact]
    public async Task SaveAsync_reports_failure_when_settings_directory_is_blocked()
    {
        var settingsPath = CreateSettingsPathWithBlockedParentDirectory(out var blockerPath);
        Exception? reported = null;
        void Handler(Exception ex) => reported = ex;
        SettingsService.SaveFailed += Handler;

        try
        {
            var service = new SettingsService(settingsPath);
            await Assert.ThrowsAnyAsync<Exception>(() => service.SaveAsync());
            Assert.NotNull(reported);
        }
        finally
        {
            SettingsService.SaveFailed -= Handler;
            DeleteIfExists(blockerPath);
        }
    }

    [Fact]
    public async Task RequestSave_reports_failure_when_settings_directory_is_blocked()
    {
        var settingsPath = CreateSettingsPathWithBlockedParentDirectory(out var blockerPath);
        Exception? reported = null;
        void Handler(Exception ex) => reported = ex;
        SettingsService.SaveFailed += Handler;

        try
        {
            var service = new SettingsService(settingsPath);
            service.RequestSave();

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (reported is null && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            Assert.NotNull(reported);
        }
        finally
        {
            SettingsService.SaveFailed -= Handler;
            DeleteIfExists(blockerPath);
        }
    }

    /// <summary>
    /// Parent path is an ordinary file, so <see cref="SettingsService"/> cannot create the settings directory.
    /// Reliable on Windows and Linux (unlike exclusive file locks or read-only targets).
    /// </summary>
    private static string CreateSettingsPathWithBlockedParentDirectory(out string blockerPath)
    {
        blockerPath = Path.Combine(Path.GetTempPath(), $"oscarwatch-blocker-{Guid.NewGuid():N}");
        File.WriteAllText(blockerPath, "blocks directory creation");
        return Path.Combine(blockerPath, "settings.json");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
