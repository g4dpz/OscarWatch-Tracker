using OscarWatch.Core.Models;
using OscarWatch.Core.Tle;

namespace OscarWatch.Core.Services;

public sealed class TleService : ITleService
{
    public const string DefaultTleUrl = "https://tle.oscarwatch.org/";

    private readonly HttpClient _httpClient;
    private List<SatelliteCatalogEntry> _catalog = [];

    public TleService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
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

    public bool IsStale(int staleHours) =>
        !LastFetchedUtc.HasValue ||
        DateTime.UtcNow - LastFetchedUtc.Value > TimeSpan.FromHours(staleHours);

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_catalog.Count > 0)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        if (File.Exists(CachePath))
        {
            var cached = await File.ReadAllTextAsync(CachePath, cancellationToken);
            _catalog = TleParser.ParseCatalog(cached).ToList();
            LastFetchedUtc = File.GetLastWriteTimeUtc(CachePath);
        }

        if (_catalog.Count == 0)
            TryLoadBundledSeed();
    }

    public async Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        var text = await _httpClient.GetStringAsync(DefaultTleUrl, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        await File.WriteAllTextAsync(CachePath, text, cancellationToken);

        _catalog = TleParser.ParseCatalog(text).ToList();
        LastFetchedUtc = DateTime.UtcNow;
    }

    public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings)
    {
        var enabled = new HashSet<string>(settings.EnabledSatelliteNames, StringComparer.OrdinalIgnoreCase);
        return _catalog.Where(s => SatelliteCatalogMatching.IsEnabled(s, enabled)).ToList();
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
