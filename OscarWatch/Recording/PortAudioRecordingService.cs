using System.Runtime.InteropServices;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using PortAudioSharp;
using Serilog;

namespace OscarWatch.Recording;

public sealed class PortAudioRecordingService : IAudioRecordingService, IDisposable
{
    /// <summary>Let PortAudio pick buffer size (required on many WASAPI devices).</summary>
    private const uint UnspecifiedFramesPerBuffer = 0;
    private const uint FallbackFramesPerBuffer = 1024;
    /// <summary>Scratch size when frame count is host-selected (variable per callback).</summary>
    private const int MaxCallbackFrameCount = 8192;
    private const int RingBufferBytes = 4 * 1024 * 1024;
    /// <summary>Log only when backlog or peak exceeds this (25% of ring).</summary>
    private const int BacklogPressureBytes = RingBufferBytes / 4;
    private static readonly TimeSpan StatsInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WriterJoinTimeout = TimeSpan.FromSeconds(5);

    private static readonly ILogger Log = Serilog.Log.ForContext<PortAudioRecordingService>();
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _streamGate = new();

    private bool _initialized;
    private string? _initError;
    private PortAudioSharp.Stream? _stream;
    private WavWriter? _wavWriter;
    private PcmRingBuffer? _ringBuffer;
    private byte[]? _callbackScratch;
    private Thread? _writerThread;
    private volatile bool _writerRunning;
    private volatile bool _captureActive;
    private readonly AutoResetEvent _dataAvailable = new(false);
    private DateTime _lastStatsUtc = DateTime.MinValue;
    private long _lastWarnedDroppedBytes;
    private int _activeChannels = 1;
    private int _maxCallbackBytes;

    public bool IsAvailable => _initialized;
    public string? UnavailableReason => _initialized ? null : _initError ?? "PortAudio is not available.";
    public bool IsRecording { get; private set; }
    public string? ActiveNoradId { get; private set; }
    public string? ActiveOutputPath { get; private set; }

    public PortAudioRecordingService()
    {
        try
        {
            PortAudio.Initialize();
            _initialized = true;
            Log.Information("PortAudio initialized ({Version})", PortAudio.VersionInfo.versionText);
        }
        catch (Exception ex)
        {
            _initError = ex.Message;
            Log.Warning(ex, "PortAudio initialization failed");
        }
    }

    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        if (!_initialized)
            return [];

        var candidates = new List<RecordingDeviceCandidate>();
        for (var i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (info.maxInputChannels <= 0)
                continue;

            candidates.Add(new RecordingDeviceCandidate(i, info.name, info.defaultLowInputLatency));
        }

        return RecordingDeviceListBuilder.Build(candidates);
    }

    public async Task StartAsync(
        string noradId,
        string satelliteName,
        string deviceId,
        RecordingFormatPreset format,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            throw new InvalidOperationException(UnavailableReason ?? "PortAudio is not available.");

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRecording)
                await StopCoreAsync().ConfigureAwait(false);

            if (!int.TryParse(deviceId, out var deviceIndex))
                throw new InvalidOperationException($"Invalid audio device id '{deviceId}'.");

            var (preferredSampleRate, channels) = format.GetFormat();
            var deviceInfo = PortAudio.GetDeviceInfo(deviceIndex);
            if (deviceInfo.maxInputChannels < channels)
                throw new InvalidOperationException(
                    $"Device '{deviceInfo.name}' does not support {channels} input channel(s).");

            _maxCallbackBytes = MaxCallbackFrameCount * channels * 2;
            _ringBuffer = new PcmRingBuffer(RingBufferBytes);
            _ringBuffer.ResetStats();
            _callbackScratch = new byte[_maxCallbackBytes];
            _activeChannels = channels;
            _lastStatsUtc = DateTime.UtcNow;
            _lastWarnedDroppedBytes = 0;

            PortAudioSharp.Stream.Callback callback = OnAudioCallback;
            int actualSampleRate;
            uint framesPerBuffer;

            lock (_streamGate)
            {
                _stream = OpenCaptureStream(
                    deviceIndex,
                    deviceInfo,
                    channels,
                    preferredSampleRate,
                    callback,
                    out actualSampleRate,
                    out framesPerBuffer);

                _wavWriter = new WavWriter(outputPath, actualSampleRate, (short)channels);
                StartWriterThread();

                _captureActive = true;
                _stream.Start();
            }

            IsRecording = true;
            ActiveNoradId = noradId;
            ActiveOutputPath = outputPath;
            Log.Information(
                "Recording started for {Satellite} ({NoradId}) -> {Path} (rate={SampleRate} Hz, frames={Frames}, ring={RingKb} KB)",
                satelliteName,
                noradId,
                outputPath,
                actualSampleRate,
                framesPerBuffer,
                RingBufferBytes / 1024);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Recording start failed for {NoradId} -> {Path}", noradId, outputPath);
            CleanupRecordingState();
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private Task StopCoreAsync()
    {
        if (!IsRecording)
            return Task.CompletedTask;

        var path = ActiveOutputPath;
        try
        {
            StopCaptureAndDrain();
            LogRecordingStats(path, final: true);
            Log.Information("Recording stopped -> {Path}", path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while stopping recording -> {Path}", path);
            CleanupRecordingState();
        }
        finally
        {
            IsRecording = false;
            ActiveNoradId = null;
            ActiveOutputPath = null;
        }

        return Task.CompletedTask;
    }

    private void StopCaptureAndDrain()
    {
        lock (_streamGate)
        {
            _captureActive = false;
            try { _stream?.Stop(); } catch (Exception ex) { Log.Debug(ex, "PortAudio stream stop"); }
            try { _stream?.Dispose(); } catch (Exception ex) { Log.Debug(ex, "PortAudio stream dispose"); }
            _stream = null;
        }

        _dataAvailable.Set();
        JoinWriterThread();

        try { _wavWriter?.Dispose(); } catch (Exception ex) { Log.Debug(ex, "WAV writer dispose"); }
        _wavWriter = null;
        _ringBuffer = null;
        _callbackScratch = null;
    }

    private static PortAudioSharp.Stream OpenCaptureStream(
        int deviceIndex,
        DeviceInfo deviceInfo,
        int channels,
        int preferredSampleRate,
        PortAudioSharp.Stream.Callback callback,
        out int actualSampleRate,
        out uint framesPerBuffer)
    {
        var input = new StreamParameters
        {
            device = deviceIndex,
            channelCount = channels,
            sampleFormat = SampleFormat.Int16,
            suggestedLatency = deviceInfo.defaultLowInputLatency,
            hostApiSpecificStreamInfo = IntPtr.Zero
        };

        var sampleRates = BuildSampleRatesToTry(preferredSampleRate, deviceInfo);
        uint[] frameBuffers = [UnspecifiedFramesPerBuffer, FallbackFramesPerBuffer];
        var failedAttempts = new List<string>();
        PortAudioException? lastError = null;

        foreach (var rate in sampleRates)
        {
            foreach (var frames in frameBuffers)
            {
                try
                {
                    var stream = new PortAudioSharp.Stream(
                        inParams: input,
                        outParams: null,
                        sampleRate: rate,
                        framesPerBuffer: frames,
                        streamFlags: StreamFlags.ClipOff,
                        callback: callback,
                        userData: IntPtr.Zero);

                    actualSampleRate = rate;
                    framesPerBuffer = frames;
                    LogStreamOpenFallback(deviceInfo.name, preferredSampleRate, rate, frames, failedAttempts);
                    return stream;
                }
                catch (PortAudioException ex)
                {
                    lastError = ex;
                    failedAttempts.Add(DescribeOpenAttempt(rate, frames));
                }
            }
        }

        var tried = string.Join("; ", failedAttempts);
        Log.Warning(
            lastError,
            "Could not open audio input on '{Device}' (tried {Attempts})",
            deviceInfo.name,
            tried);
        throw new InvalidOperationException(
            $"Could not open audio input on '{deviceInfo.name}'. Try another device or recording format.",
            lastError);
    }

    private static string DescribeOpenAttempt(int sampleRate, uint frames) =>
        frames == UnspecifiedFramesPerBuffer
            ? $"{sampleRate} Hz"
            : $"{sampleRate} Hz, {frames}-frame buffer";

    private static void LogStreamOpenFallback(
        string deviceName,
        int preferredSampleRate,
        int actualSampleRate,
        uint framesPerBuffer,
        IReadOnlyList<string> failedAttempts)
    {
        if (actualSampleRate != preferredSampleRate)
        {
            Log.Information(
                "Using {Rate} Hz on '{Device}' ({Requested} Hz not supported on this device)",
                actualSampleRate,
                deviceName,
                preferredSampleRate);
            return;
        }

        if (framesPerBuffer != UnspecifiedFramesPerBuffer)
        {
            Log.Information(
                "Opened '{Device}' at {Rate} Hz with {Frames}-frame buffers (host default buffer size was rejected)",
                deviceName,
                actualSampleRate,
                framesPerBuffer);
            return;
        }

        if (failedAttempts.Count > 0)
        {
            Log.Information(
                "Opened '{Device}' at {Rate} Hz after {AttemptCount} failed open attempt(s): {Attempts}",
                deviceName,
                actualSampleRate,
                failedAttempts.Count,
                string.Join("; ", failedAttempts));
        }
    }

    private static List<int> BuildSampleRatesToTry(int preferredSampleRate, DeviceInfo deviceInfo)
    {
        var rates = new List<int>();
        if (preferredSampleRate > 0)
            rates.Add(preferredSampleRate);

        var deviceRate = (int)Math.Round(deviceInfo.defaultSampleRate);
        if (deviceRate > 0 && !rates.Contains(deviceRate))
            rates.Add(deviceRate);

        if (rates.Count == 0)
            rates.Add(44100);

        return rates;
    }

    private StreamCallbackResult OnAudioCallback(
        IntPtr inputPtr,
        IntPtr outputPtr,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userData)
    {
        if (!_captureActive)
            return StreamCallbackResult.Complete;

        if ((statusFlags & StreamCallbackFlags.InputOverflow) != 0)
            Log.Warning("PortAudio input overflow reported in recording callback");

        var ring = _ringBuffer;
        var scratch = _callbackScratch;
        if (ring is null || scratch is null)
            return StreamCallbackResult.Complete;

        var sampleCount = (int)frameCount * _activeChannels;
        var byteCount = sampleCount * 2;
        if (byteCount > scratch.Length)
        {
            Log.Warning(
                "Recording callback frame larger than scratch buffer ({ByteCount} > {Capacity}); dropping",
                byteCount,
                scratch.Length);
            ring.RecordDropped(byteCount);
            return StreamCallbackResult.Continue;
        }

        Marshal.Copy(inputPtr, scratch, 0, byteCount);
        ring.TryWrite(scratch.AsSpan(0, byteCount));
        _dataAvailable.Set();
        return StreamCallbackResult.Continue;
    }

    private void StartWriterThread()
    {
        _writerRunning = true;
        _writerThread = new Thread(WriterLoop)
        {
            IsBackground = true,
            Name = "OscarWatch.RecordingWriter"
        };
        _writerThread.Start();
    }

    private void JoinWriterThread()
    {
        _writerRunning = false;
        _dataAvailable.Set();

        var thread = _writerThread;
        _writerThread = null;
        if (thread is null)
            return;

        if (!thread.Join(WriterJoinTimeout))
            Log.Warning("Recording writer thread did not exit within {TimeoutSeconds}s", WriterJoinTimeout.TotalSeconds);
    }

    private void WriterLoop()
    {
        var drainBuffer = new byte[64 * 1024];
        try
        {
            while (true)
            {
                _dataAvailable.WaitOne(250);

                var writer = _wavWriter;
                var ring = _ringBuffer;
                if (writer is not null && ring is not null)
                {
                    int read;
                    while ((read = ring.Read(drainBuffer)) > 0)
                        writer.WritePcm16(drainBuffer.AsSpan(0, read));

                    MaybeLogPeriodicStats();
                }

                if (!_writerRunning && !_captureActive && !HasBufferedAudio())
                    break;
            }

            FlushRemainingAudio();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Recording writer thread failed");
        }
    }

    private void FlushRemainingAudio()
    {
        var writer = _wavWriter;
        var ring = _ringBuffer;
        if (writer is null || ring is null)
            return;

        var drainBuffer = new byte[64 * 1024];
        int read;
        while ((read = ring.Read(drainBuffer)) > 0)
            writer.WritePcm16(drainBuffer.AsSpan(0, read));
    }

    private bool HasBufferedAudio() => _ringBuffer?.Count > 0;

    private void MaybeLogPeriodicStats()
    {
        if (!IsRecording)
            return;

        var now = DateTime.UtcNow;
        if (now - _lastStatsUtc < StatsInterval)
            return;

        var ring = _ringBuffer;
        if (ring is null)
            return;

        if (!ShouldLogRecordingPressure(ring, periodic: true, _lastWarnedDroppedBytes))
            return;

        _lastStatsUtc = now;
        LogRecordingPressure(ActiveOutputPath, ring, final: false);
    }

    private static bool ShouldLogRecordingPressure(PcmRingBuffer ring, bool periodic, long lastWarnedDroppedBytes)
    {
        if (periodic)
        {
            if (ring.DroppedBytes > lastWarnedDroppedBytes)
                return true;

            return ring.Count >= BacklogPressureBytes;
        }

        return ring.DroppedBytes > 0 || ring.PeakCount >= BacklogPressureBytes;
    }

    private void LogRecordingStats(string? path, bool final)
    {
        var ring = _ringBuffer;
        if (ring is null)
            return;

        if (!ShouldLogRecordingPressure(ring, periodic: false, _lastWarnedDroppedBytes))
            return;

        LogRecordingPressure(path, ring, final);
    }

    private void LogRecordingPressure(string? path, PcmRingBuffer ring, bool final)
    {
        var droppedKb = ring.DroppedBytes / 1024.0;
        var backlogKb = ring.Count / 1024.0;
        var peakKb = ring.PeakCount / 1024.0;
        _lastWarnedDroppedBytes = ring.DroppedBytes;

        if (final)
        {
            Log.Warning(
                "Recording buffer pressure (final) {Path}: peak backlog {PeakKb:F1} KB, dropped {DroppedKb:F1} KB",
                path,
                peakKb,
                droppedKb);
            return;
        }

        Log.Warning(
            "Recording buffer pressure {Path}: backlog {BacklogKb:F1} KB, peak {PeakKb:F1} KB, dropped {DroppedKb:F1} KB",
            path,
            backlogKb,
            peakKb,
            droppedKb);
    }

    private void CleanupRecordingState()
    {
        try
        {
            StopCaptureAndDrain();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Recording cleanup failed");
        }

        IsRecording = false;
        ActiveNoradId = null;
        ActiveOutputPath = null;
    }

    public void Dispose()
    {
        try
        {
            StopCoreAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Recording dispose stop failed");
        }

        if (_initialized)
        {
            try
            {
                PortAudio.Terminate();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "PortAudio terminate failed");
            }

            _initialized = false;
            Log.Information("PortAudio terminated");
        }

        _dataAvailable.Dispose();
        _operationLock.Dispose();
    }
}
