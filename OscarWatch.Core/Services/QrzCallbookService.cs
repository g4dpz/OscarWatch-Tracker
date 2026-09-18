using System.Net;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Net;
using OscarWatch.Core.Qrz;

namespace OscarWatch.Core.Services;

public sealed class QrzCallbookService : IQrzCallbookService
{
    public const string XmlEndpoint = "https://xmldata.qrz.com/xml/current/";

    private static readonly TimeSpan HitTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan MissTtl = TimeSpan.FromHours(1);

    private readonly HttpClient _httpClient;
    private readonly object _sessionLock = new();
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CachedLookup> _cache = new(StringComparer.OrdinalIgnoreCase);

    private string? _sessionKey;
    private string? _sessionUsername;
    private string? _sessionPassword;

    public QrzCallbookService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? OscarWatchHttpClients.Create(TimeSpan.FromSeconds(8));
    }

    public bool CanLookup(QrzSettings? settings) =>
        settings is { Enabled: true } && settings.HasCredentials;

    public async Task<QrzConnectionTestResult> TestConnectionAsync(
        QrzSettings settings,
        CancellationToken cancellationToken = default)
    {
        var username = settings.Username?.Trim() ?? "";
        var password = settings.Password ?? "";
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return QrzConnectionTestResult.Failed("missing-credentials");

        try
        {
            var session = await LoginAsync(username, password, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(session.Error))
            {
                return QrzConnectionTestResult.Failed(
                    session.Error,
                    QrzXmlParser.IsSubscriptionRequired(session.Error));
            }

            if (!session.HasKey)
                return QrzConnectionTestResult.Failed(session.Message.Length > 0 ? session.Message : "no-session");

            StoreSession(username, password, session.Key);

            if (QrzCallsignHelper.IsPlausible(username))
            {
                var probe = await LookupRemoteAsync(session.Key, QrzCallsignHelper.ToLookupCall(username), cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(probe.Session.Error)
                    && QrzXmlParser.IsSubscriptionRequired(probe.Session.Error))
                {
                    return QrzConnectionTestResult.Failed(probe.Session.Error, subscriptionRequired: true);
                }
            }

            return QrzConnectionTestResult.Success(
                string.IsNullOrWhiteSpace(session.SubscriptionExpires) ? null : session.SubscriptionExpires);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return QrzConnectionTestResult.Failed("timeout");
        }
        catch (HttpRequestException ex)
        {
            return QrzConnectionTestResult.Failed(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return QrzConnectionTestResult.Failed(ex.Message);
        }
    }

    public async Task<QrzCallbookEntry?> LookupAsync(
        QrzSettings settings,
        string callsign,
        CancellationToken cancellationToken = default)
    {
        if (!CanLookup(settings))
            return null;

        var lookupCall = QrzCallsignHelper.ToLookupCall(callsign);
        if (!QrzCallsignHelper.IsPlausible(lookupCall))
            return null;

        if (TryGetCached(lookupCall, out var cached))
            return cached;

        var username = settings.Username.Trim();
        var password = settings.Password;
        try
        {
            var sessionKey = await EnsureSessionAsync(username, password, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(sessionKey))
                return null;

            var remote = await LookupRemoteAsync(sessionKey, lookupCall, cancellationToken).ConfigureAwait(false);
            if (QrzXmlParser.IsSessionTimeout(remote.Session.Error))
            {
                ClearSession();
                sessionKey = await EnsureSessionAsync(username, password, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(sessionKey))
                    return null;

                remote = await LookupRemoteAsync(sessionKey, lookupCall, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(remote.Session.Error))
            {
                if (QrzXmlParser.IsNotFound(remote.Session.Error))
                    StoreCache(lookupCall, null, hit: false);

                return null;
            }

            var entry = NormalizeEntry(remote.Entry);
            StoreCache(lookupCall, entry, hit: entry is not null);
            return entry;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static QrzCallbookEntry? NormalizeEntry(QrzCallbookEntry? entry)
    {
        if (entry is null)
            return null;

        var name = entry.Name.Trim();
        var grid = "";
        if (!string.IsNullOrWhiteSpace(entry.Grid)
            && MaidenheadLocator.TryValidateGrids(entry.Grid, out var normalizedGrid, out _, out _))
        {
            grid = normalizedGrid;
        }

        if (name.Length == 0 && grid.Length == 0)
            return null;

        return new QrzCallbookEntry
        {
            Call = entry.Call,
            Name = name,
            Grid = grid
        };
    }

    private async Task<string?> EnsureSessionAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        lock (_sessionLock)
        {
            if (!string.IsNullOrWhiteSpace(_sessionKey)
                && string.Equals(_sessionUsername, username, StringComparison.Ordinal)
                && string.Equals(_sessionPassword, password, StringComparison.Ordinal))
            {
                return _sessionKey;
            }
        }

        var session = await LoginAsync(username, password, cancellationToken).ConfigureAwait(false);
        if (!session.HasKey || !string.IsNullOrWhiteSpace(session.Error))
        {
            ClearSession();
            return null;
        }

        StoreSession(username, password, session.Key);
        return session.Key;
    }

    private async Task<QrzXmlSession> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var url = XmlEndpoint
            + "?username=" + Uri.EscapeDataString(username)
            + "&password=" + Uri.EscapeDataString(password)
            + "&agent=" + Uri.EscapeDataString(OscarWatchHttpClients.ProductName);
        var xml = await GetXmlAsync(url, cancellationToken).ConfigureAwait(false);
        return QrzXmlParser.ParseSession(xml);
    }

    private async Task<(QrzCallbookEntry? Entry, QrzXmlSession Session)> LookupRemoteAsync(
        string sessionKey,
        string callsign,
        CancellationToken cancellationToken)
    {
        var url = XmlEndpoint
            + "?s=" + Uri.EscapeDataString(sessionKey)
            + "&callsign=" + Uri.EscapeDataString(callsign);
        var xml = await GetXmlAsync(url, cancellationToken).ConfigureAwait(false);
        return (QrzXmlParser.ParseCallsign(xml), QrzXmlParser.ParseSession(xml));
    }

    private async Task<string> GetXmlAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return "";
        if (!response.IsSuccessStatusCode)
            return body;

        return body;
    }

    private void StoreSession(string username, string password, string key)
    {
        lock (_sessionLock)
        {
            _sessionKey = key;
            _sessionUsername = username;
            _sessionPassword = password;
        }
    }

    private void ClearSession()
    {
        lock (_sessionLock)
        {
            _sessionKey = null;
            _sessionUsername = null;
            _sessionPassword = null;
        }
    }

    private bool TryGetCached(string call, out QrzCallbookEntry? entry)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(call, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
            {
                entry = cached.Entry;
                return true;
            }
        }

        entry = null;
        return false;
    }

    private void StoreCache(string call, QrzCallbookEntry? entry, bool hit)
    {
        var ttl = hit ? HitTtl : MissTtl;
        lock (_cacheLock)
        {
            _cache[call] = new CachedLookup(entry, DateTime.UtcNow.Add(ttl));
        }
    }

    private readonly record struct CachedLookup(QrzCallbookEntry? Entry, DateTime ExpiresUtc);
}
