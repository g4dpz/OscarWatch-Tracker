namespace OscarWatch.Core.Models;

public sealed class PassRecordingSettings
{
    public bool Enabled { get; set; }
    /// <summary>Durable PortAudio device name (not a volatile enumeration index).</summary>
    public string DeviceId { get; set; } = "";
    public string DeviceDisplayName { get; set; } = "";
    public RecordingFormatPreset Format { get; set; } = RecordingFormatPreset.Mono44100;
    /// <summary>Preferred file container. MP3 requires ffmpeg on PATH; otherwise WAV is kept.</summary>
    public RecordingContainerFormat Container { get; set; } = RecordingContainerFormat.Wav;
    public double StartElevationDeg { get; set; } = 5.0;
    public double StopElevationDeg { get; set; } = 3.0;
    public string OutputFolder { get; set; } = "";

    /// <summary>
    /// Clears legacy numeric PortAudio indices so rematch uses <see cref="DeviceDisplayName"/>.
    /// </summary>
    public void MigrateLegacyNumericDeviceId()
    {
        var id = DeviceId?.Trim() ?? "";
        if (id.Length == 0)
            return;

        for (var i = 0; i < id.Length; i++)
        {
            if (!char.IsAsciiDigit(id[i]))
                return;
        }

        DeviceId = "";
    }
}
