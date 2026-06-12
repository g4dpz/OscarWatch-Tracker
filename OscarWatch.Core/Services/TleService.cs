using OscarWatch.Core.Models;
using OscarWatch.Core.Net;
using OscarWatch.Core.Tle;

namespace OscarWatch.Core.Services;

public sealed class TleService : ITleService
{
    public const string DefaultTleUrl = "https://tle.oscarwatch.org/";

    private readonly ISettingsService? _settings;
    private readonly HttpClient _httpClient;
    private List<SatelliteCatalogEntry> _catalog = [];
    private string? _loadedSourceKey;

    public TleService(ISettingsService? settings = null, HttpClient? httpClient = null)
    {
        _settings = settings;
        _httpClient = httpClient ?? OscarWatchHttpClients.Create(TimeSpan.FromSeconds(30));
    }

    public IReadOnlyList<SatelliteCatalogEntry> Catalog => _catalog;
    public DateTime? LastFetchedUtc { get; private set; }

    public string CachePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OscarWatch",
        "tle-cache.txt");

    public static string BundledSeedPath { get; } = Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "tle-seed.txt");

    public string ActiveSourceLabel => TleSourceResolver.GetDisplayLabel(EffectiveSettings);

    public bool IsStale(int staleHours) =>
        !LastFetchedUtc.HasValue ||
        DateTime.UtcNow - LastFetchedUtc.Value > TimeSpan.FromHours(staleHours);

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        var settings = EffectiveSettings;
        var sourceKey = TleSourceResolver.GetSourceKey(settings);
        if (_catalog.Count > 0 && string.Equals(_loadedSourceKey, sourceKey, StringComparison.Ordinal))
            return;

        _catalog = [];
        _loadedSourceKey = sourceKey;
        LastFetchedUtc = null;

        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        if (File.Exists(CachePath))
        {
            var cached = await File.ReadAllTextAsync(CachePath, cancellationToken);
            _catalog = TleParser.ParseCatalog(cached).ToList();
            LastFetchedUtc = File.GetLastWriteTimeUtc(CachePath);
        }

        if (_catalog.Count > 0)
            return;

        if (TleSourceResolver.TryGetLocalFilePath(settings) is { } localPath)
        {
            TryLoadFromFile(localPath);
            return;
        }

        if (settings.Mode == TleSourceMode.OscarWatch)
            TryLoadBundledSeed();
    }

    public async Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        var settings = EffectiveSettings;
        if (TleSourceResolver.TryGetLocalFilePath(settings) is { } localPath)
        {
            if (!File.Exists(localPath))
                throw new FileNotFoundException($"TLE file not found: {localPath}");

            var localText = await File.ReadAllTextAsync(localPath, cancellationToken);
            ApplyCatalog(localText, fromNetwork: false);
            return;
        }

        var url = TleSourceResolver.TryGetNetworkUrl(settings);
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Enter a TLE download URL in Settings → TLE.");

        var text = await _httpClient.GetStringAsync(url, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        await File.WriteAllTextAsync(CachePath, text, cancellationToken);
        ApplyCatalog(text, fromNetwork: true);
    }

    public void InvalidateCatalog()
    {
        _catalog = [];
        _loadedSourceKey = null;
        LastFetchedUtc = null;

        if (File.Exists(CachePath))
        {
            try
            {
                File.Delete(CachePath);
            }
            catch
            {
                // best effort — stale cache is preferable to a crash
            }
        }
    }

    public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings)
    {
        var enabled = new HashSet<string>(settings.EnabledSatelliteNames, StringComparer.OrdinalIgnoreCase);
        return _catalog.Where(s => SatelliteCatalogMatching.IsEnabled(s, enabled)).ToList();
    }

    private TleSourceSettings EffectiveSettings =>
        _settings?.Current.TleSource ?? new TleSourceSettings();

    private void ApplyCatalog(string text, bool fromNetwork)
    {
        _catalog = TleParser.ParseCatalog(text).ToList();
        _loadedSourceKey = TleSourceResolver.GetSourceKey(EffectiveSettings);
        LastFetchedUtc = fromNetwork ? DateTime.UtcNow : File.Exists(CachePath)
            ? File.GetLastWriteTimeUtc(CachePath)
            : DateTime.UtcNow;
    }

    private void TryLoadFromFile(string path)
    {
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path);
        _catalog = TleParser.ParseCatalog(text).ToList();
        if (_catalog.Count > 0)
            LastFetchedUtc = File.GetLastWriteTimeUtc(path);
    }

    private void TryLoadBundledSeed()
    {
        if (!File.Exists(BundledSeedPath))
            return;

        var text = File.ReadAllText(BundledSeedPath);
        _catalog = TleParser.ParseCatalog(text).ToList();
        if (_catalog.Count > 0)
            LastFetchedUtc = null;
    }
}
