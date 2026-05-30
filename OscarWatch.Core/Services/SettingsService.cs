using System.Diagnostics;
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

    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public SettingsService(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OscarWatch",
            "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    public string SettingsPath { get; }

    public static event Action<Exception>? SaveFailed;

    public void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        if (!File.Exists(SettingsPath))
        {
            Current = new AppSettings();
            SyncGridFromLatLon();
            EnsureSavedStations();
            SaveToDisk();
            return;
        }

        var json = File.ReadAllText(SettingsPath);
        Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        Current.GroundStation ??= new GroundStation();
        Current.VoiceAnnouncements ??= new VoiceAnnouncementSettings();
        Current.FrequencySelections ??= new Dictionary<string, SatelliteFrequencySelection>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in Current.FrequencySelections.Values)
        {
            selection.ModeOffsets ??= new Dictionary<string, ModeOffsetSettings>(StringComparer.OrdinalIgnoreCase);
            selection.CwUplinkByMode ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            selection.CwReceiveOffsetKHzByMode ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            selection.DopplerStrategyByMode ??= new Dictionary<string, DopplerStrategy>(StringComparer.OrdinalIgnoreCase);
        }
        Current.Rotator ??= new RotatorSettings();
        Current.Rig ??= new RigSettings();
        Current.Cloudlog ??= new CloudlogSettings();
        Current.PassRecording ??= new PassRecordingSettings();
        EnsureSavedStations();

        if (string.IsNullOrWhiteSpace(Current.GroundStation.GridSquare))
            SyncGridFromLatLon();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Load(), cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(SaveToDisk, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportSaveFailed(ex);
            throw;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public void RequestSave()
    {
        _ = SaveAsync().ContinueWith(
            static _ => { },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
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

    private void SaveToDisk()
    {
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        WriteAtomic(SettingsPath, json);
    }

    internal static void WriteAtomic(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        WriteAllTextWithRetry(tempPath, contents);
        ReplaceFileWithRetry(tempPath, path);
    }

    private static void WriteAllTextWithRetry(string path, string contents)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.WriteAllText(path, contents);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(25 * attempt);
            }
        }
    }

    private static void ReplaceFileWithRetry(string sourcePath, string destinationPath)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(destinationPath))
                    File.Replace(sourcePath, destinationPath, destinationPath + ".bak", ignoreMetadataErrors: true);
                else
                    File.Move(sourcePath, destinationPath);

                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(25 * attempt);
            }
        }
    }

    private static void ReportSaveFailed(Exception ex)
    {
        Trace.TraceError("OscarWatch settings save failed: {0}", ex.Message);
        SaveFailed?.Invoke(ex);
    }
}
