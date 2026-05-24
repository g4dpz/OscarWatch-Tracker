using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed record AudioInputDevice(string Id, string DisplayName);

public interface IAudioRecordingService
{
    bool IsAvailable { get; }
    string? UnavailableReason { get; }
    bool IsRecording { get; }
    string? ActiveNoradId { get; }
    string? ActiveOutputPath { get; }

    IReadOnlyList<AudioInputDevice> GetInputDevices();
    Task StartAsync(
        string noradId,
        string satelliteName,
        string deviceId,
        RecordingFormatPreset format,
        string outputPath,
        CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
