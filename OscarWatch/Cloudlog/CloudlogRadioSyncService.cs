using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Cloudlog;

public sealed class CloudlogRadioSyncService : ICloudlogRadioSyncService
{
    private readonly CloudlogRadioClient _client = new();
    private readonly object _gate = new();
    private string? _lastSignature;
    private DateTime _lastPostUtc = DateTime.MinValue;
    private string? _lastError;
    private DateTimeOffset? _lastSuccessUtc;
    private int _inFlight;

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

        var minInterval = Math.Max(250, settings.MinUpdateIntervalMs);
        lock (_gate)
        {
            if (string.Equals(_lastSignature, update.Signature, StringComparison.Ordinal)
                && (DateTime.UtcNow - _lastPostUtc).TotalMilliseconds < minInterval)
                return;

            if (_inFlight > 0)
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
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _inFlight--;
                _lastError = ex.Message;
            }
        }
    }
}
