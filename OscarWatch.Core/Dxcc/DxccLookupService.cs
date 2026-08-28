using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using OscarWatch.Core.Net;

namespace OscarWatch.Core.Dxcc;

public sealed class DxccLookupService : IDxccLookupService
{
    private static readonly Regex BigCtyZipHref = new(
        @"https?://www\.country-files\.com/bigcty/download/\d{4}/bigcty-\d{8}\.zip",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _appBaseDirectory;
    private readonly HttpClient _httpClient;
    private readonly object _gate = new();

    private CallsignDxccResolver? _resolver;
    private DxccEntityMap? _entityMap;
    private string? _loadedCountryPath;

    public DxccLookupService(string? appBaseDirectory = null, HttpClient? httpClient = null)
    {
        _appBaseDirectory = appBaseDirectory ?? AppContext.BaseDirectory;
        _httpClient = httpClient ?? OscarWatchHttpClients.Create(TimeSpan.FromSeconds(60));
    }

    public string ActiveCountryFilePath
    {
        get
        {
            EnsureLoaded();
            return _loadedCountryPath ?? ResolveCountryFilePath();
        }
    }

    public DateTime? CountryFileLastWriteUtc
    {
        get
        {
            var path = ActiveCountryFilePath;
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        }
    }

    public bool TryResolve(string? callsign, out DxccMatch match)
    {
        match = default;
        EnsureLoaded();
        if (_resolver is null || _entityMap is null)
            return false;

        if (!_resolver.TryMatch(callsign, out var ctyMatch))
            return false;

        if (!_entityMap.TryResolve(ctyMatch.Entity, out var info))
            return false;

        match = new DxccMatch(info.Dxcc, info.Country, ctyMatch.Entity.PrimaryPrefix, ctyMatch.Entity.Name);
        return true;
    }

    public void EnsureLoaded()
    {
        lock (_gate)
        {
            if (_resolver is not null && _entityMap is not null)
                return;

            LoadUnlocked();
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _resolver = null;
            _entityMap = null;
            _loadedCountryPath = null;
            LoadUnlocked();
        }
    }

    public async Task<DxccCountryFileUpdateResult> UpdateCountryFileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var zipUrl = await ResolveLatestZipUrlAsync(cancellationToken).ConfigureAwait(false);
            await using var zipStream = await _httpClient.GetStreamAsync(zipUrl, cancellationToken).ConfigureAwait(false);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var entry = archive.Entries.FirstOrDefault(e =>
                e.Name.Equals("cty.dat", StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return new DxccCountryFileUpdateResult
                {
                    Success = false,
                    Message = "The downloaded archive did not contain cty.dat."
                };
            }

            await using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            // Validate before replacing the user file.
            _ = CtyDatParser.Parse(text);
            if (!text.Contains(':', StringComparison.Ordinal))
            {
                return new DxccCountryFileUpdateResult
                {
                    Success = false,
                    Message = "The downloaded cty.dat file was not valid."
                };
            }

            var userPath = CtyDatPaths.UserCountryFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(userPath)!);
            var tempPath = userPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, text, cancellationToken).ConfigureAwait(false);
            File.Copy(tempPath, userPath, overwrite: true);
            File.Delete(tempPath);

            Reload();

            return new DxccCountryFileUpdateResult
            {
                Success = true,
                Message = "Country file updated.",
                SavedPath = userPath
            };
        }
        catch (Exception ex)
        {
            return new DxccCountryFileUpdateResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private void LoadUnlocked()
    {
        try
        {
            var countryPath = ResolveCountryFilePath();
            var mapPath = CtyDatPaths.BundledEntityMapPath(_appBaseDirectory);

            if (!File.Exists(countryPath) || !File.Exists(mapPath))
            {
                _resolver = null;
                _entityMap = null;
                _loadedCountryPath = File.Exists(countryPath) ? countryPath : null;
                return;
            }

            var database = CtyDatParser.ParseFile(countryPath);
            _entityMap = DxccEntityMap.LoadFromJsonFile(mapPath);
            _resolver = new CallsignDxccResolver(database);
            _loadedCountryPath = countryPath;
        }
        catch
        {
            _resolver = null;
            _entityMap = null;
            _loadedCountryPath = null;
        }
    }

    private string ResolveCountryFilePath()
    {
        var userPath = CtyDatPaths.UserCountryFilePath;
        if (File.Exists(userPath))
            return userPath;

        return CtyDatPaths.BundledCountryFilePath(_appBaseDirectory);
    }

    private async Task<string> ResolveLatestZipUrlAsync(CancellationToken cancellationToken)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(
                "https://www.country-files.com/category/big-cty/",
                cancellationToken).ConfigureAwait(false);
            var matches = BigCtyZipHref.Matches(html);
            if (matches.Count > 0)
            {
                // Prefer the lexicographically latest dated zip (YYYYMMDD in the filename).
                return matches
                    .Select(m => m.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(u => u, StringComparer.OrdinalIgnoreCase)
                    .First();
            }
        }
        catch
        {
            // Fall back to the bundled known URL.
        }

        return CtyDatPaths.RemoteBigCtyZipUrl;
    }
}
