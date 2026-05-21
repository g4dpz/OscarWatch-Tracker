using System.Text.Json;
using System.Text.Json.Serialization;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public AppSettings Current { get; private set; } = new();

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OscarWatch",
        "settings.json");

    public void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        if (!File.Exists(SettingsPath))
        {
            Current = new AppSettings();
            SyncGridFromLatLon();
            EnsureSavedStations();
            Save();
            return;
        }

        var json = File.ReadAllText(SettingsPath);
        Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        Current.GroundStation ??= new GroundStation();
        Current.VoiceAnnouncements ??= new VoiceAnnouncementSettings();
        Current.FrequencySelections ??= new Dictionary<string, SatelliteFrequencySelection>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in Current.FrequencySelections.Values)
            selection.ModeOffsets ??= new Dictionary<string, ModeOffsetSettings>(StringComparer.OrdinalIgnoreCase);
        EnsureSavedStations();

        if (string.IsNullOrWhiteSpace(Current.GroundStation.GridSquare))
            SyncGridFromLatLon();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Load(), cancellationToken).ConfigureAwait(false);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public void EnsureSavedStations()
    {
        if (Current.SavedStations.Count == 0)
        {
            var home = StationProfile.FromGroundStation(Current.GroundStation);
            Current.SavedStations.Add(home);
            Current.ActiveStationId = home.Id;
        }

        if (string.IsNullOrWhiteSpace(Current.ActiveStationId))
            Current.ActiveStationId = Current.SavedStations[0].Id;

        ApplyActiveStation();
    }

    public void ApplyActiveStation()
    {
        var profile = Current.SavedStations.FirstOrDefault(s => s.Id == Current.ActiveStationId)
            ?? Current.SavedStations.FirstOrDefault();
        if (profile is null)
            return;

        Current.ActiveStationId = profile.Id;
        Current.GroundStation = profile.ToGroundStation();
    }

    public void SyncActiveStationFromGroundStation()
    {
        var profile = Current.SavedStations.FirstOrDefault(s => s.Id == Current.ActiveStationId);
        if (profile is null)
            return;

        profile.DisplayName = Current.GroundStation.DisplayName;
        profile.LatitudeDeg = Current.GroundStation.LatitudeDeg;
        profile.LongitudeDeg = Current.GroundStation.LongitudeDeg;
        profile.AltitudeMetersAsl = Current.GroundStation.AltitudeMetersAsl;
        profile.GridSquare = Current.GroundStation.GridSquare;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Save(), cancellationToken);
    }

    public void SyncGridFromLatLon()
    {
        Current.GroundStation.GridSquare = MaidenheadGrid.FromLatLon(
            Current.GroundStation.LatitudeDeg,
            Current.GroundStation.LongitudeDeg);
    }

    public void SyncLatLonFromGrid()
    {
        var (lat, lon) = MaidenheadGrid.ToLatLonCenter(Current.GroundStation.GridSquare);
        Current.GroundStation.LatitudeDeg = lat;
        Current.GroundStation.LongitudeDeg = lon;
    }
}
