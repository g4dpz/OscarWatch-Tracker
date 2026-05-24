using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class PassRecordingCoordinatorTests
{
    private readonly PassRecordingCoordinator _coordinator = new();
    private readonly FakeAudioRecordingService _recording = new();

    private static PassRecordingSettings EnabledSettings => new()
    {
        Enabled = true,
        DeviceId = "0",
        StartElevationDeg = 5,
        StopElevationDeg = 3,
        Format = RecordingFormatPreset.Mono44100
    };

    private static SatelliteTrackState State(string noradId, double elevationDeg) => new()
    {
        NoradId = noradId,
        Name = "SO-50",
        Subpoint = new GeoCoordinate(0, 0, 400),
        LookAngles = new LookAngles(0, elevationDeg, 1000)
    };

    [Fact]
    public void Starts_on_rising_edge_for_focused_satellite()
    {
        var utc = DateTime.UtcNow;
        _coordinator.Process("25544", State("25544", 4.0), EnabledSettings, _recording, utc);
        _coordinator.Process("25544", State("25544", 6.0), EnabledSettings, _recording, utc);

        Assert.True(_recording.IsRecording);
        Assert.Equal("25544", _recording.ActiveNoradId);
        Assert.Contains("so-50-", _recording.ActiveOutputPath!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stops_on_falling_edge_below_stop_threshold()
    {
        var utc = DateTime.UtcNow;
        _coordinator.Process("25544", State("25544", 4.0), EnabledSettings, _recording, utc);
        _coordinator.Process("25544", State("25544", 6.0), EnabledSettings, _recording, utc);
        _coordinator.Process("25544", State("25544", 2.0), EnabledSettings, _recording, utc);

        Assert.False(_recording.IsRecording);
        Assert.Equal(1, _recording.StopCount);
    }

    [Fact]
    public void Does_not_start_when_not_focused_or_disabled()
    {
        var utc = DateTime.UtcNow;
        var disabled = new PassRecordingSettings
        {
            Enabled = false,
            DeviceId = "0",
            StartElevationDeg = 5,
            StopElevationDeg = 3
        };

        _coordinator.Process(null, State("25544", 6.0), EnabledSettings, _recording, utc);
        _coordinator.Process("25544", State("25544", 6.0), disabled, _recording, utc);
        _coordinator.Process("25544", null, EnabledSettings, _recording, utc);

        Assert.False(_recording.IsRecording);
        Assert.Equal(0, _recording.StartCount);
    }

    [Fact]
    public void Starts_immediately_when_focused_satellite_already_above_start_elevation()
    {
        var utc = DateTime.UtcNow;
        _coordinator.Process("25544", State("25544", 20.0), EnabledSettings, _recording, utc);

        Assert.True(_recording.IsRecording);
        Assert.Equal("25544", _recording.ActiveNoradId);
    }

    [Fact]
    public void Switches_recording_when_focus_changes_to_satellite_already_above_threshold()
    {
        var utc = DateTime.UtcNow;
        _coordinator.Process("25544", State("25544", 4.0), EnabledSettings, _recording, utc);
        _coordinator.Process("25544", State("25544", 6.0), EnabledSettings, _recording, utc);

        _coordinator.Process("99999", State("99999", 8.0), EnabledSettings, _recording, utc);

        Assert.True(_recording.IsRecording);
        Assert.Equal("99999", _recording.ActiveNoradId);
        Assert.Equal(1, _recording.StopCount);
        Assert.Equal(2, _recording.StartCount);
    }

    [Fact]
    public void Stops_when_focus_changes_to_satellite_below_start_elevation()
    {
        var utc = DateTime.UtcNow;
        _coordinator.Process("25544", State("25544", 4.0), EnabledSettings, _recording, utc);
        _coordinator.Process("25544", State("25544", 6.0), EnabledSettings, _recording, utc);

        _coordinator.Process("99999", State("99999", 2.0), EnabledSettings, _recording, utc);

        Assert.False(_recording.IsRecording);
        Assert.Equal(1, _recording.StopCount);
    }
}

internal sealed class FakeAudioRecordingService : IAudioRecordingService
{
    public bool IsAvailable => true;
    public string? UnavailableReason => null;
    public bool IsRecording { get; private set; }
    public string? ActiveNoradId { get; private set; }
    public string? ActiveOutputPath { get; private set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }

    public IReadOnlyList<AudioInputDevice> GetInputDevices() =>
        [new AudioInputDevice("0", "Fake Input")];

    public Task StartAsync(
        string noradId,
        string satelliteName,
        string deviceId,
        RecordingFormatPreset format,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        StartCount++;
        IsRecording = true;
        ActiveNoradId = noradId;
        ActiveOutputPath = outputPath;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCount++;
        IsRecording = false;
        ActiveNoradId = null;
        ActiveOutputPath = null;
        return Task.CompletedTask;
    }
}
