namespace OscarWatch.Core.Models;

public enum RecordingFormatPreset
{
    Mono44100,
    Mono48000,
    Stereo44100
}

public static class RecordingFormatPresetExtensions
{
    public static (int SampleRate, int Channels) GetFormat(this RecordingFormatPreset preset) =>
        preset switch
        {
            RecordingFormatPreset.Mono48000 => (48000, 1),
            RecordingFormatPreset.Stereo44100 => (44100, 2),
            _ => (44100, 1)
        };

    public static string GetLabel(this RecordingFormatPreset preset) =>
        preset switch
        {
            RecordingFormatPreset.Mono48000 => "Mono 48 kHz",
            RecordingFormatPreset.Stereo44100 => "Stereo 44.1 kHz",
            _ => "Mono 44.1 kHz"
        };
}
