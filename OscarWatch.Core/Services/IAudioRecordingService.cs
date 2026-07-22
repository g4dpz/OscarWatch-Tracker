using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed record AudioInputDevice(string Id, string DisplayName);

public interface IAudioRecordingService
{
    /// <summary>True before any probe, or after a successful PortAudio initialisation.</summary>
    bool IsAvailable { get; }
    /// <summary>Non-null only after an initialisation attempt failed.</summary>
    string? UnavailableReason { get; }
    bool IsRecording { get; }
    string? ActiveNoradId { get; }
    string? ActiveOutputPath { get; }

    /// <summary>Initialises PortAudio when recording or device enumeration is needed.</summary>
    bool TryInitialize();

    IReadOnlyList<AudioInputDevice> GetInputDevices();
    Task StartAsync(
        string noradId,
        string satelliteName,
        string deviceId,
        RecordingFormatPreset format,
        string outputPath,
        string? deviceName = null,
        CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
