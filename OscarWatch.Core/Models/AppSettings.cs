namespace OscarWatch.Core.Models;

public sealed class AppSettings
{
    public GroundStation GroundStation { get; set; } = new();
    public string ActiveStationId { get; set; } = "";
    public List<StationProfile> SavedStations { get; set; } = [];
    public List<string> EnabledSatelliteNames { get; set; } = ["ISS", "SO-50", "AO-91"];
    public double MinimumElevationDeg { get; set; } = 5.0;
    public int PassPredictionHours { get; set; } = 48;
    public int PassFilterMinDurationMinutes { get; set; } = 2;
    public int TleStaleHours { get; set; } = 6;
    public TleAutoUpdateMode TleAutoUpdate { get; set; } = TleAutoUpdateMode.OnStartup;
    public AppThemePreference Theme { get; set; } = AppThemePreference.System;
    public VoiceAnnouncementSettings VoiceAnnouncements { get; set; } = new();
    public Dictionary<string, SatelliteFrequencySelection> FrequencySelections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double FrequencyOverlayX { get; set; } = 12;
    public double FrequencyOverlayY { get; set; } = 12;
    public RotatorSettingsStub Rotator { get; set; } = new();
    public RigSettingsStub Rig { get; set; } = new();
}

public sealed class RotatorSettingsStub
{
    public bool Enabled { get; set; }
    public string Port { get; set; } = "";
    public string Type { get; set; } = "";
    public double MinAzimuthDeg { get; set; }
    public double MaxAzimuthDeg { get; set; } = 360;
    public double MinElevationDeg { get; set; }
    public double MaxElevationDeg { get; set; } = 90;
}

public sealed class RigSettingsStub
{
    public bool Enabled { get; set; }
    public string Port { get; set; } = "";
    public string Model { get; set; } = "";
}
