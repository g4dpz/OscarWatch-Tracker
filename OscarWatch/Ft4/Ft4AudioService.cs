using System.Collections.Concurrent;
using OscarWatch.Core.Services;
using OscarWatch.Recording;
using PortAudioSharp;
using Serilog;
using PaStream = PortAudioSharp.Stream;

namespace OscarWatch.Ft4;

/// <summary>PortAudio capture + playback for the FT4 modem (separate from pass recording).</summary>
public sealed class Ft4AudioService : IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<Ft4AudioService>();
    private readonly object _gate = new();
    private readonly ConcurrentQueue<float> _captureRing = new();
    private readonly float[] _monitorRing = new float[8192];
    private int _monitorWrite;
    private int _monitorCount;
    private readonly object _monitorGate = new();
    private PaStream? _input;
    private PaStream? _output;
    private float[]? _playback;
    private int _playbackIndex;
    private bool _portAudioReady;
    private int _captureSampleRate = 48000;
    private int _playbackSampleRate = 48000;

    public bool IsAvailable
    {
        get
        {
            EnsurePortAudio();
            return _portAudioReady;
        }
    }

    public int CaptureSampleRate => _captureSampleRate;
    public int PlaybackSampleRate => _playbackSampleRate;

    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        EnsurePortAudio();
        if (!_portAudioReady)
            return [];

        var candidates = new List<RecordingDeviceCandidate>();
        for (var i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (info.maxInputChannels <= 0)
                continue;
            candidates.Add(new RecordingDeviceCandidate(
                i,
                info.name ?? $"Input {i}",
                info.defaultLowInputLatency));
        }

        return RecordingDeviceListBuilder.Build(candidates);
    }

    public IReadOnlyList<AudioInputDevice> GetOutputDevices()
    {
        EnsurePortAudio();
        if (!_portAudioReady)
            return [];

        var candidates = new List<RecordingDeviceCandidate>();
        for (var i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (info.maxOutputChannels <= 0)
                continue;
            candidates.Add(new RecordingDeviceCandidate(
                i,
                info.name ?? $"Output {i}",
                info.defaultLowOutputLatency));
        }

        return RecordingDeviceListBuilder.Build(candidates);
    }

    public void StartCapture(string? deviceId, string? deviceDisplayName = null)
    {
        lock (_gate)
        {
            EnsurePortAudio();
            if (!_portAudioReady)
                throw new InvalidOperationException("PortAudio is not available.");

            StopCaptureUnlocked();
            while (_captureRing.TryDequeue(out _))
            {
            }

            var deviceIndex = ResolveDeviceIndex(deviceId, deviceDisplayName, input: true);
            if (deviceIndex < 0)
            {
                throw new InvalidOperationException(
                    "FT4 input soundcard is no longer available. " +
                    "Open FT4 Settings, click Refresh, and re-select the input device.");
            }

            var info = PortAudio.GetDeviceInfo(deviceIndex);
            _captureSampleRate = (int)Math.Round(info.defaultSampleRate);
            if (_captureSampleRate < 8000)
                _captureSampleRate = 48000;

            var param = new StreamParameters
            {
                device = deviceIndex,
                channelCount = 1,
                sampleFormat = SampleFormat.Float32,
                suggestedLatency = info.defaultLowInputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            _input = new PaStream(
                inParams: param,
                outParams: null,
                sampleRate: _captureSampleRate,
                framesPerBuffer: 256,
                streamFlags: StreamFlags.ClipOff,
                callback: OnInput,
                userData: IntPtr.Zero);
            _input.Start();
            Log.Information(
                "FT4 capture started on '{Device}' (index {Index}) at {Rate} Hz",
                info.name,
                deviceIndex,
                _captureSampleRate);
        }
    }

    public void StopCapture()
    {
        lock (_gate)
            StopCaptureUnlocked();
    }

    public int ReadCaptureSamples(Span<float> destination)
    {
        var n = 0;
        while (n < destination.Length && _captureRing.TryDequeue(out var sample))
            destination[n++] = sample;
        return n;
    }

    /// <summary>Copy the most recent monitor samples without consuming the decode queue.</summary>
    public int CopyMonitorSamples(Span<float> destination)
    {
        lock (_monitorGate)
        {
            var n = Math.Min(destination.Length, _monitorCount);
            if (n == 0)
                return 0;

            var start = (_monitorWrite - n + _monitorRing.Length) % _monitorRing.Length;
            for (var i = 0; i < n; i++)
                destination[i] = _monitorRing[(start + i) % _monitorRing.Length];
            return n;
        }
    }

    public void PlayPcm(float[] samples12k, double level, string? deviceId, string? deviceDisplayName = null)
    {
        lock (_gate)
        {
            EnsurePortAudio();
            if (!_portAudioReady)
                throw new InvalidOperationException("PortAudio is not available.");

            StopPlaybackUnlocked();

            var deviceIndex = ResolveDeviceIndex(deviceId, deviceDisplayName, input: false);
            if (deviceIndex < 0)
            {
                throw new InvalidOperationException(
                    "FT4 output soundcard is no longer available. " +
                    "Open FT4 Settings, click Refresh, and re-select the output device.");
            }

            var info = PortAudio.GetDeviceInfo(deviceIndex);
            _playbackSampleRate = (int)Math.Round(info.defaultSampleRate);
            if (_playbackSampleRate < 8000)
                _playbackSampleRate = 48000;

            var scaled = Resample(samples12k, 12000, _playbackSampleRate);
            var gain = (float)Math.Clamp(level, 0.01, 1.0);
            for (var i = 0; i < scaled.Length; i++)
                scaled[i] *= gain;

            _playback = scaled;
            _playbackIndex = 0;

            var param = new StreamParameters
            {
                device = deviceIndex,
                channelCount = 1,
                sampleFormat = SampleFormat.Float32,
                suggestedLatency = info.defaultLowOutputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            _output = new PaStream(
                inParams: null,
                outParams: param,
                sampleRate: _playbackSampleRate,
                framesPerBuffer: 256,
                streamFlags: StreamFlags.ClipOff,
                callback: OnOutput,
                userData: IntPtr.Zero);
            _output.Start();
        }
    }

    public bool IsPlaying
    {
        get
        {
            lock (_gate)
                return _playback is not null && _playbackIndex < _playback.Length;
        }
    }

    public void StopPlayback()
    {
        lock (_gate)
            StopPlaybackUnlocked();
    }

    private StreamCallbackResult OnInput(
        IntPtr input,
        IntPtr output,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userData)
    {
        if (input == IntPtr.Zero)
            return StreamCallbackResult.Continue;

        unsafe
        {
            var ptr = (float*)input.ToPointer();
            for (var i = 0; i < frameCount; i++)
            {
                var sample = ptr[i];
                _captureRing.Enqueue(sample);
                // Bound memory if the consumer falls behind.
                while (_captureRing.Count > _captureSampleRate * 20)
                    _captureRing.TryDequeue(out _);
            }

            lock (_monitorGate)
            {
                for (var i = 0; i < frameCount; i++)
                {
                    _monitorRing[_monitorWrite] = ptr[i];
                    _monitorWrite = (_monitorWrite + 1) % _monitorRing.Length;
                    if (_monitorCount < _monitorRing.Length)
                        _monitorCount++;
                }
            }
        }

        return StreamCallbackResult.Continue;
    }

    private StreamCallbackResult OnOutput(
        IntPtr input,
        IntPtr output,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userData)
    {
        if (output == IntPtr.Zero)
            return StreamCallbackResult.Complete;

        unsafe
        {
            var ptr = (float*)output.ToPointer();
            var src = _playback;
            if (src is null)
            {
                for (var i = 0; i < frameCount; i++)
                    ptr[i] = 0;
                return StreamCallbackResult.Complete;
            }

            for (var i = 0; i < frameCount; i++)
            {
                if (_playbackIndex < src.Length)
                    ptr[i] = src[_playbackIndex++];
                else
                    ptr[i] = 0;
            }

            if (_playbackIndex >= src.Length)
            {
                _playback = null;
                return StreamCallbackResult.Complete;
            }
        }

        return StreamCallbackResult.Continue;
    }

    private void StopCaptureUnlocked()
    {
        try { _input?.Stop(); } catch { /* ignore */ }
        try { _input?.Dispose(); } catch { /* ignore */ }
        _input = null;
    }

    private void StopPlaybackUnlocked()
    {
        try { _output?.Stop(); } catch { /* ignore */ }
        try { _output?.Dispose(); } catch { /* ignore */ }
        _output = null;
        _playback = null;
        _playbackIndex = 0;
    }

    private void EnsurePortAudio()
    {
        if (_portAudioReady)
            return;

        try
        {
            if (!PortAudioOutOfProcessProbe.TryRun(out _))
            {
                _portAudioReady = false;
                return;
            }

            PortAudio.Initialize();
            _portAudioReady = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "FT4 PortAudio init failed");
            _portAudioReady = false;
        }
    }

    private static int ResolveDeviceIndex(string? deviceId, string? deviceDisplayName, bool input)
    {
        if (string.IsNullOrWhiteSpace(deviceId) && string.IsNullOrWhiteSpace(deviceDisplayName))
            return input ? PortAudio.DefaultInputDevice : PortAudio.DefaultOutputDevice;

        var snapshots = new List<RecordingDeviceResolver.InputDeviceSnapshot>();
        for (var i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            var channels = input ? info.maxInputChannels : info.maxOutputChannels;
            if (channels <= 0)
                continue;
            var latency = input ? info.defaultLowInputLatency : info.defaultLowOutputLatency;
            snapshots.Add(new RecordingDeviceResolver.InputDeviceSnapshot(
                i,
                info.name ?? "",
                latency,
                channels));
        }

        return RecordingDeviceResolver.ResolveIndex(deviceId, deviceDisplayName, snapshots);
    }

    private static float[] Resample(float[] input, int inRate, int outRate)
    {
        if (inRate == outRate)
            return (float[])input.Clone();

        var outLen = (int)((long)input.Length * outRate / inRate);
        var output = new float[outLen];
        for (var i = 0; i < outLen; i++)
        {
            var srcPos = i * (double)inRate / outRate;
            var i0 = (int)srcPos;
            var i1 = Math.Min(i0 + 1, input.Length - 1);
            var frac = srcPos - i0;
            output[i] = (float)(input[i0] * (1 - frac) + input[i1] * frac);
        }

        return output;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            StopCaptureUnlocked();
            StopPlaybackUnlocked();
        }
    }
}
