using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using Serilog;

namespace OscarWatch.Cloudlog;

public sealed class CloudlogRadioSyncService : ICloudlogRadioSyncService
{
    private static readonly ILogger Log = Serilog.Log.ForContext<CloudlogRadioSyncService>();
    private readonly CloudlogRadioClient _client = new();
    private readonly object _gate = new();
    private string? _lastSignature;
    private DateTime _lastPostUtc = DateTime.MinValue;
    private string? _lastError;
    private DateTimeOffset? _lastSuccessUtc;
    private int _inFlight;

    public event Action? StateChanged;

    public string? LastError
    {
        get { lock (_gate) return _lastError; }
    }

    public DateTimeOffset? LastSuccessUtc
    {
        get { lock (_gate) return _lastSuccessUtc; }
    }

    public void ResetThrottle()
    {
        lock (_gate)
        {
            _lastSignature = null;
            _lastPostUtc = DateTime.MinValue;
        }
    }

    public void Publish(CloudlogSettings settings, CloudlogRadioUpdate? update)
    {
        if (!settings.Enabled || update is null)
            return;

        if (string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrWhiteSpace(settings.ApiKey))
            return;

        lock (_gate)
        {
            if (_inFlight > 0)
                return;

            if (!CloudlogRadioPublishPolicy.ShouldPost(
                    _lastSignature,
                    update.Signature,
                    _lastPostUtc,
                    DateTime.UtcNow,
                    settings.MinUpdateIntervalMs))
                return;

            _inFlight++;
            _lastSignature = update.Signature;
            _lastPostUtc = DateTime.UtcNow;
        }

        _ = PostAsync(settings, update);
    }

    public async Task<bool> TestConnectionAsync(CloudlogSettings settings, CancellationToken cancellationToken = default)
    {
        var probe = new CloudlogRadioUpdate("TEST", 145_825_000, 435_850_000, "FM", "FM");
        var (ok, error) = await _client.PostRadioAsync(settings, probe, cancellationToken).ConfigureAwait(false);
        lock (_gate)
            _lastError = ok ? null : error;

        NotifyStateChanged();
        return ok;
    }

    private async Task PostAsync(CloudlogSettings settings, CloudlogRadioUpdate update)
    {
        try
        {
            var (ok, error) = await _client.PostRadioAsync(settings, update).ConfigureAwait(false);
            lock (_gate)
            {
                _inFlight--;
                if (ok)
                {
                    _lastError = null;
                    _lastSuccessUtc = DateTimeOffset.UtcNow;
                }
                else
                    _lastError = error;
            }

            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Cloudlog radio post failed");
            lock (_gate)
            {
                _inFlight--;
                _lastError = ex.Message;
            }

            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        try
        {
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Cloudlog state changed handler failed");
        }
    }
}
