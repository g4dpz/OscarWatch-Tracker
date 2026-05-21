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
    public RotatorSettings Rotator { get; set; } = new();
    public RigSettings Rig { get; set; } = new();
}
