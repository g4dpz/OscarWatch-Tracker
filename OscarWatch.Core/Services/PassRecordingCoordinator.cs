using OscarWatch.Core.Display;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed class PassRecordingCoordinator
{
    private readonly IRecordingTaskScheduler _tasks;
    private bool _hasSample;
    private double _previousElevationDeg = -90.0;
    private string? _trackedNoradId;
    private int _belowStopTicks;

    /// <summary>
    /// Require this many consecutive below-stop samples (~1 Hz ticks) before ending a pass recording.
    /// Avoids stopping on a single noisy elevation sample mid-pass.
    /// </summary>
    public const int BelowStopConfirmTicks = 3;

    public PassRecordingCoordinator(IRecordingTaskScheduler? taskScheduler = null) =>
        _tasks = taskScheduler ?? DefaultRecordingTaskScheduler.Instance;

    public void Process(
        string? focusedNoradId,
        SatelliteTrackState? focusedState,
        PassRecordingSettings settings,
        IAudioRecordingService recording,
        DateTime utcNow)
    {
        if (AudioRecordingSessions.IsManualTest(recording))
            return;

        if (recording.IsRecording
            && !string.IsNullOrEmpty(recording.ActiveNoradId)
            && !string.Equals(recording.ActiveNoradId, focusedNoradId, StringComparison.Ordinal))
        {
            _tasks.Schedule(() => recording.StopAsync(), "stop recording (focus changed)");
            ResetTracking();
        }

        if (!settings.Enabled
            || string.IsNullOrWhiteSpace(focusedNoradId)
            || (string.IsNullOrWhiteSpace(settings.DeviceId)
                && string.IsNullOrWhiteSpace(settings.DeviceDisplayName)))
        {
            ResetTracking();
            return;
        }

        // Focused sat briefly missing from the live snapshot (propagation glitch): keep elevation
        // history and leave any active recording alone so REC does not stop/restart.
        if (focusedState is null
            || !string.Equals(focusedState.NoradId, focusedNoradId, StringComparison.Ordinal))
            return;

        if (!string.Equals(_trackedNoradId, focusedNoradId, StringComparison.Ordinal))
        {
            ResetTracking();
            _trackedNoradId = focusedNoradId;
        }

        // Propagation can miss a tick; do not treat missing look angles as -90° or we
        // stop the recording and restart a few seconds later (REC → Passing → REC).
        if (focusedState.LookAngles is null)
            return;

        var elevation = focusedState.LookAngles.ElevationDeg;
        var stopThreshold = settings.StopElevationDeg;
        var startThreshold = settings.StartElevationDeg;

        if (elevation < stopThreshold)
        {
            _belowStopTicks++;
            if (_belowStopTicks >= BelowStopConfirmTicks
                && recording.IsRecording
                && string.Equals(recording.ActiveNoradId, focusedNoradId, StringComparison.Ordinal))
            {
                _tasks.Schedule(() => recording.StopAsync(), "stop recording (below stop elevation)");
            }
        }
        else
        {
            _belowStopTicks = 0;
            if (!recording.IsRecording)
            {
                var crossedStart = _hasSample
                    && _previousElevationDeg < startThreshold
                    && elevation >= startThreshold;
                var alreadyAboveOnFirstSample = !_hasSample && elevation >= startThreshold;
                if (crossedStart || alreadyAboveOnFirstSample)
                    TryStartRecording(focusedNoradId, focusedState, settings, recording, utcNow, _tasks);
            }
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
        DateTime utcNow,
        IRecordingTaskScheduler tasks)
    {
        var outputFolder = RecordingFileNameFormat.ResolveOutputFolder(settings.OutputFolder);
        var preferredPath = RecordingFileNameFormat.ResolveUniquePath(
            outputFolder,
            focusedState.Name,
            utcNow,
            settings.Container);
        var capturePath = RecordingFileNameFormat.GetCaptureWavPath(preferredPath);
        tasks.Schedule(
            () => recording.StartAsync(
                focusedNoradId,
                focusedState.Name,
                settings.DeviceId,
                settings.Format,
                capturePath,
                settings.DeviceDisplayName,
                settings.Container),
            "start pass recording");
    }

    public void ResetTracking()
    {
        _hasSample = false;
        _previousElevationDeg = -90.0;
        _trackedNoradId = null;
        _belowStopTicks = 0;
    }
}
