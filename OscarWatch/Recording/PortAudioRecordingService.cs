using System.Runtime.InteropServices;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using PortAudioSharp;
using Serilog;

namespace OscarWatch.Recording;

public sealed class PortAudioRecordingService : IAudioRecordingService, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<PortAudioRecordingService>();
    private readonly object _sync = new();
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private bool _initialized;
    private string? _initError;
    private PortAudioSharp.Stream? _stream;
    private WavWriter? _wavWriter;
    private byte[]? _callbackBuffer;

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

        var devices = new List<AudioInputDevice>();
        for (var i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (info.maxInputChannels <= 0)
                continue;

            devices.Add(new AudioInputDevice(i.ToString(), info.name));
        }

        return devices;
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

            var (sampleRate, channels) = format.GetFormat();
            var deviceInfo = PortAudio.GetDeviceInfo(deviceIndex);
            if (deviceInfo.maxInputChannels < channels)
                throw new InvalidOperationException(
                    $"Device '{deviceInfo.name}' does not support {channels} input channel(s).");

            var input = new StreamParameters
            {
                device = deviceIndex,
                channelCount = channels,
                sampleFormat = SampleFormat.Int16,
                suggestedLatency = deviceInfo.defaultLowInputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            _wavWriter = new WavWriter(outputPath, sampleRate, (short)channels);
            _callbackBuffer = new byte[8192];
            _activeChannels = channels;

            PortAudioSharp.Stream.Callback callback = OnAudioCallback;

            _stream = new PortAudioSharp.Stream(
                inParams: input,
                outParams: null,
                sampleRate: sampleRate,
                framesPerBuffer: 0,
                streamFlags: StreamFlags.ClipOff,
                callback: callback,
                userData: IntPtr.Zero);

            _stream.Start();
            IsRecording = true;
            ActiveNoradId = noradId;
            ActiveOutputPath = outputPath;
            Log.Information(
                "Recording started for {Satellite} ({NoradId}) -> {Path}",
                satelliteName,
                noradId,
                outputPath);
        }
        catch
        {
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

        try
        {
            lock (_sync)
            {
                _stream?.Stop();
                _stream?.Dispose();
                _stream = null;
                _wavWriter?.Dispose();
                _wavWriter = null;
                _callbackBuffer = null;
            }

            Log.Information("Recording stopped -> {Path}", ActiveOutputPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while stopping recording");
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

    private int _activeChannels = 1;

    private StreamCallbackResult OnAudioCallback(
        IntPtr inputPtr,
        IntPtr outputPtr,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userData)
    {
        lock (_sync)
        {
            if (_wavWriter is null)
                return StreamCallbackResult.Complete;

            var sampleCount = (int)frameCount * _activeChannels;
            var byteCount = sampleCount * 2;
            if (_callbackBuffer is null || _callbackBuffer.Length < byteCount)
                _callbackBuffer = new byte[byteCount];

            Marshal.Copy(inputPtr, _callbackBuffer, 0, byteCount);
            _wavWriter.WritePcm16(_callbackBuffer.AsSpan(0, byteCount));
            return StreamCallbackResult.Continue;
        }
    }

    private void CleanupRecordingState()
    {
        lock (_sync)
        {
            try { _stream?.Stop(); } catch { /* ignore */ }
            try { _stream?.Dispose(); } catch { /* ignore */ }
            _stream = null;
            try { _wavWriter?.Dispose(); } catch { /* ignore */ }
            _wavWriter = null;
            _callbackBuffer = null;
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
        catch
        {
            // ignore shutdown errors
        }

        if (_initialized)
        {
            try
            {
                PortAudio.Terminate();
            }
            catch
            {
                // ignore
            }

            _initialized = false;
        }

        _operationLock.Dispose();
    }
}
