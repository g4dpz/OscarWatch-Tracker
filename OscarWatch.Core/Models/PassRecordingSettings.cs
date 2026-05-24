namespace OscarWatch.Core.Models;

public sealed class PassRecordingSettings
{
    public bool Enabled { get; set; }
    public string DeviceId { get; set; } = "";
    public string DeviceDisplayName { get; set; } = "";
    public RecordingFormatPreset Format { get; set; } = RecordingFormatPreset.Mono44100;
    public double StartElevationDeg { get; set; } = 5.0;
    public double StopElevationDeg { get; set; } = 3.0;
    public string OutputFolder { get; set; } = "";
}
