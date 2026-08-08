namespace OscarWatch.Core.Models;

public enum RecordingContainerFormat
{
    Wav,
    Mp3
}

public static class RecordingContainerFormatExtensions
{
    public static string GetExtension(this RecordingContainerFormat container) =>
        container == RecordingContainerFormat.Mp3 ? ".mp3" : ".wav";

    public static string GetLabel(this RecordingContainerFormat container) =>
        container == RecordingContainerFormat.Mp3 ? "MP3" : "WAV";
}
