using System.Net;
using System.Text;
using OscarWatch.Core.Geo;
using OscarWatch.Core.HamQth;
using OscarWatch.Core.Models;
using OscarWatch.Core.Net;
using OscarWatch.Core.Qrz;

namespace OscarWatch.Core.Services;

public sealed class HamQthCallbookService : IHamQthCallbookService
{
    public const string XmlEndpoint = "https://www.hamqth.com/xml.php";

    private static readonly TimeSpan HitTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan MissTtl = TimeSpan.FromHours(1);

    private readonly HttpClient _httpClient;
    private readonly object _sessionLock = new();
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CachedLookup> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Reusable StringBuilder buffer for URL construction to avoid string concatenation allocations
    private readonly StringBuilder _urlBuffer = new(256);

    private string? _sessionId;
    private string? _sessionUsername;
    private string? _sessionPassword;

    public HamQthCallbookService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? OscarWatchHttpClients.Create(TimeSpan.FromSeconds(8));
    }

    public bool CanLookup(HamQthSettings? settings) =>
        settings is { Enabled: true } && settings.HasCredentials;

    public async Task<HamQthConnectionTestResult> TestConnectionAsync(
        HamQthSettings settings,
        CancellationToken cancellationToken = default)
    {
        var username = settings.Username?.Trim() ?? "";
        var password = settings.Password ?? "";
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return HamQthConnectionTestResult.Failed("missing-credentials");

        try
        {
            var session = await LoginAsync(username, password, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(session.Error))
                return HamQthConnectionTestResult.Failed(session.Error);

            if (!session.HasSession)
                return HamQthConnectionTestResult.Failed("no-session");

            StoreSession(username, password, session.SessionId);
            return HamQthConnectionTestResult.Success();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HamQthConnectionTestResult.Failed("timeout");
        }
        catch (HttpRequestException ex)
        {
            return HamQthConnectionTestResult.Failed(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HamQthConnectionTestResult.Failed(ex.Message);
        }
    }

    public async Task<QrzCallbookEntry?> LookupAsync(
        HamQthSettings settings,
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
            var sessionId = await EnsureSessionAsync(username, password, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(sessionId))
                return null;

            var remote = await LookupRemoteAsync(sessionId, lookupCall, cancellationToken).ConfigureAwait(false);
            if (HamQthXmlParser.IsSessionExpired(remote.Session.Error))
            {
                ClearSession();
                sessionId = await EnsureSessionAsync(username, password, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(sessionId))
                    return null;

                remote = await LookupRemoteAsync(sessionId, lookupCall, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(remote.Session.Error))
            {
                if (HamQthXmlParser.IsNotFound(remote.Session.Error))
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
            if (!string.IsNullOrWhiteSpace(_sessionId)
                && string.Equals(_sessionUsername, username, StringComparison.Ordinal)
                && string.Equals(_sessionPassword, password, StringComparison.Ordinal))
            {
                return _sessionId;
            }
        }

        var session = await LoginAsync(username, password, cancellationToken).ConfigureAwait(false);
        if (!session.HasSession || !string.IsNullOrWhiteSpace(session.Error))
        {
            ClearSession();
            return null;
        }

        StoreSession(username, password, session.SessionId);
        return session.SessionId;
    }

    private async Task<HamQthXmlSession> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var url = BuildLoginUrl(username, password);
        var xml = await GetXmlAsync(url, cancellationToken).ConfigureAwait(false);
        return HamQthXmlParser.ParseSession(xml);
    }

    private async Task<(QrzCallbookEntry? Entry, HamQthXmlSession Session)> LookupRemoteAsync(
        string sessionId,
        string callsign,
        CancellationToken cancellationToken)
    {
        var url = BuildLookupUrl(sessionId, callsign);
        var xml = await GetXmlAsync(url, cancellationToken).ConfigureAwait(false);
        return (HamQthXmlParser.ParseSearch(xml), HamQthXmlParser.ParseSession(xml));
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

    private void StoreSession(string username, string password, string sessionId)
    {
        lock (_sessionLock)
        {
            _sessionId = sessionId;
            _sessionUsername = username;
            _sessionPassword = password;
        }
    }

    private void ClearSession()
    {
        lock (_sessionLock)
        {
            _sessionId = null;
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

    /// <summary>
    /// Builds login URL using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 4 string allocations per login call.
    /// </summary>
    private string BuildLoginUrl(string username, string password)
    {
        lock (_urlBuffer)
        {
            _urlBuffer.Clear();
            _urlBuffer.Append(XmlEndpoint);
            _urlBuffer.Append("?u=");
            _urlBuffer.Append(Uri.EscapeDataString(username));
            _urlBuffer.Append("&p=");
            _urlBuffer.Append(Uri.EscapeDataString(password));
            return _urlBuffer.ToString();
        }
    }

    /// <summary>
    /// Builds lookup URL using reusable buffer to avoid string concatenation allocations.
    /// Eliminates 6 string allocations per lookup call.
    /// </summary>
    private string BuildLookupUrl(string sessionId, string callsign)
    {
        lock (_urlBuffer)
        {
            _urlBuffer.Clear();
            _urlBuffer.Append(XmlEndpoint);
            _urlBuffer.Append("?id=");
            _urlBuffer.Append(Uri.EscapeDataString(sessionId));
            _urlBuffer.Append("&callsign=");
            _urlBuffer.Append(Uri.EscapeDataString(callsign));
            _urlBuffer.Append("&prg=");
            _urlBuffer.Append(Uri.EscapeDataString(OscarWatchHttpClients.ProductName));
            return _urlBuffer.ToString();
        }
    }

    private readonly record struct CachedLookup(QrzCallbookEntry? Entry, DateTime ExpiresUtc);
}
