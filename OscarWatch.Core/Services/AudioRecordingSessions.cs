namespace OscarWatch.Core.Services;

public static class AudioRecordingSessions
{
    public const string ManualTestNoradId = "TEST";

    public static bool IsManualTest(IAudioRecordingService recording) =>
        recording.IsRecording
        && string.Equals(recording.ActiveNoradId, ManualTestNoradId, StringComparison.Ordinal);
}
