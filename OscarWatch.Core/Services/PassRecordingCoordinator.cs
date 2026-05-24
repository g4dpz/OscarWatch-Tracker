using OscarWatch.Core.Display;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed class PassRecordingCoordinator
{
    private bool _hasSample;
    private double _previousElevationDeg = -90.0;
    private string? _trackedNoradId;

    public void Process(
        string? focusedNoradId,
        SatelliteTrackState? focusedState,
        PassRecordingSettings settings,
        IAudioRecordingService recording,
        DateTime utcNow)
    {
        if (recording.IsRecording
            && !string.IsNullOrEmpty(recording.ActiveNoradId)
            && !string.Equals(recording.ActiveNoradId, focusedNoradId, StringComparison.Ordinal))
        {
            _ = recording.StopAsync();
            ResetTracking();
        }

        if (!settings.Enabled
            || string.IsNullOrWhiteSpace(focusedNoradId)
            || string.IsNullOrWhiteSpace(settings.DeviceId)
            || focusedState is null
            || !string.Equals(focusedState.NoradId, focusedNoradId, StringComparison.Ordinal))
        {
            ResetTracking();
            return;
        }

        if (!string.Equals(_trackedNoradId, focusedNoradId, StringComparison.Ordinal))
        {
            ResetTracking();
            _trackedNoradId = focusedNoradId;
        }

        var elevation = focusedState.LookAngles?.ElevationDeg ?? -90.0;
        var stopThreshold = settings.StopElevationDeg;
        var startThreshold = settings.StartElevationDeg;

        if (elevation < stopThreshold)
        {
            if (recording.IsRecording
                && string.Equals(recording.ActiveNoradId, focusedNoradId, StringComparison.Ordinal))
                _ = recording.StopAsync();
        }
        else if (!recording.IsRecording)
        {
            var crossedStart = _hasSample
                && _previousElevationDeg < startThreshold
                && elevation >= startThreshold;
            var alreadyAboveOnFirstSample = !_hasSample && elevation >= startThreshold;
            if (crossedStart || alreadyAboveOnFirstSample)
                TryStartRecording(focusedNoradId, focusedState, settings, recording, utcNow);
        }

        if (!_hasSample)
            _hasSample = true;

        _previousElevationDeg = elevation;
    }

    private static void TryStartRecording(
        string focusedNoradId,
        SatelliteTrackState focusedState,
        PassRecordingSettings settings,
        IAudioRecordingService recording,
        DateTime utcNow)
    {
        var outputFolder = RecordingFileNameFormat.ResolveOutputFolder(settings.OutputFolder);
        var outputPath = RecordingFileNameFormat.ResolveUniquePath(
            outputFolder,
            focusedState.Name,
            utcNow);
        _ = recording.StartAsync(
            focusedNoradId,
            focusedState.Name,
            settings.DeviceId,
            settings.Format,
            outputPath);
    }

    public void ResetTracking()
    {
        _hasSample = false;
        _previousElevationDeg = -90.0;
        _trackedNoradId = null;
    }
}
