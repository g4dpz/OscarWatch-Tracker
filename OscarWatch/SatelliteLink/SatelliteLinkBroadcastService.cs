using System.Text.Json;
using OscarWatch.Core.Models;
using OscarWatch.Core.SatelliteLink;
using OscarWatch.Core.Services;
using Serilog;

namespace OscarWatch.SatelliteLink;

public sealed class SatelliteLinkBroadcastService : ISatelliteLinkBroadcastService
{
    private static readonly ILogger Log = Serilog.Log.ForContext<SatelliteLinkBroadcastService>();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly SatelliteLinkWebSocketHost _host = new();
    private readonly object _gate = new();
    private SatelliteLinkSettings _settings = new();
    private string? _lastSignature;
    private DateTime _lastBroadcastUtc = DateTime.MinValue;
    private string? _lastError;

    public event Action? StateChanged;

    public bool IsListening => _host.IsListening;

    public int ClientCount => _host.ClientCount;

    public string? LastError
    {
        get
        {
            lock (_gate)
                return _lastError ?? _host.LastError;
        }
    }

    public SatelliteLinkBroadcastService()
    {
        _host.StateChanged += () => StateChanged?.Invoke();
    }

    public void ApplySettings(SatelliteLinkSettings settings)
    {
        _settings = settings ?? new SatelliteLinkSettings();
        _ = ApplySettingsAsync(_settings);
    }

    public void Publish(SatelliteTrackState? track, RigTrackingContext? context, bool force = false)
    {
        if (!_settings.Enabled || !_host.IsListening)
            return;

        var message = context is not null
            ? SatelliteLinkMessageBuilder.Build(context, _settings.OnlyWhenInRange, DateTime.UtcNow)
            : track is not null && !string.IsNullOrWhiteSpace(track.Name)
                ? BuildNameOnly(track)
                : SatelliteLinkMessageBuilder.BuildEmpty(DateTime.UtcNow);

        var signature = message.Signature;
        var now = DateTime.UtcNow;

        lock (_gate)
        {
            if (!SatelliteLinkPublishPolicy.ShouldBroadcast(
                    _lastSignature,
                    signature,
                    _lastBroadcastUtc,
                    now,
                    _settings.UpdateIntervalMs,
                    force))
                return;

            _lastSignature = signature;
            _lastBroadcastUtc = now;
        }

        var json = JsonSerializer.Serialize(message, JsonOptions);
        _ = BroadcastAsync(json);
    }

    public async Task<bool> TestBindAsync(SatelliteLinkSettings settings, CancellationToken cancellationToken = default)
    {
        var probe = new SatelliteLinkSettings
        {
            Enabled = true,
            Port = settings.Port,
            AllowLanClients = settings.AllowLanClients
        };

        var wasListening = _host.IsListening;
        var resumeSettings = _settings;

        if (wasListening)
            await _host.StopAsync().ConfigureAwait(false);

        await using var host = new SatelliteLinkWebSocketHost();
        try
        {
            await host.StartAsync(probe, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            lock (_gate)
                _lastError = SatelliteLinkListenPrefixBuilder.DescribeBindFailure(ex);
            StateChanged?.Invoke();
            Log.Debug(ex, "Satellite link bind test failed");
            return false;
        }
        finally
        {
            await host.StopAsync().ConfigureAwait(false);

            if (wasListening && resumeSettings.Enabled)
            {
                try
                {
                    await _host.StartAsync(resumeSettings, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (_gate)
                        _lastError = SatelliteLinkListenPrefixBuilder.DescribeBindFailure(ex);
                    Log.Warning(ex, "Satellite link failed to resume after bind test");
                    StateChanged?.Invoke();
                }
            }
        }
    }

    public Task StopAsync() => _host.StopAsync();

    private async Task ApplySettingsAsync(SatelliteLinkSettings settings)
    {
        lock (_gate)
        {
            _lastSignature = null;
            _lastBroadcastUtc = DateTime.MinValue;
            _lastError = null;
        }

        await _host.StopAsync().ConfigureAwait(false);

        if (!settings.Enabled)
        {
            StateChanged?.Invoke();
            return;
        }

        try
        {
            await _host.StartAsync(settings).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (_gate)
                _lastError = SatelliteLinkListenPrefixBuilder.DescribeBindFailure(ex);
            Log.Warning(ex, "Satellite link failed to start");
        }

        StateChanged?.Invoke();
    }

    private async Task BroadcastAsync(string json)
    {
        try
        {
            await _host.BroadcastAsync(json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (_gate)
                _lastError = ex.Message;
            Log.Debug(ex, "Satellite link broadcast failed");
            StateChanged?.Invoke();
        }
    }

    private static SatelliteLinkMessage BuildNameOnly(SatelliteTrackState track)
    {
        var look = track.LookAngles;
        return new SatelliteLinkMessage
        {
            TimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            InRange = true,
            Satellite = new SatelliteLinkSatelliteInfo
            {
                Name = track.Name.Trim(),
                NoradId = track.NoradId
            },
            Tracking = look is null
                ? null
                : new SatelliteLinkTrackingInfo
                {
                    AzimuthDeg = look.AzimuthDeg,
                    ElevationDeg = look.ElevationDeg,
                    RangeKm = look.RangeKm,
                    RangeRateKmPerSec = look.RangeRateKmPerSec,
                    IsSunlit = track.IsSunlit
                },
            WispDde = WispDdeFormatter.Format(
                track.Name,
                look,
                0,
                0,
                "",
                "")
        };
    }
}
