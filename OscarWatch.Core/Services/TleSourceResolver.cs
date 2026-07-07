using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public static class TleSourceResolver
{
    public const string OscarWatchGpJsonUrl = "https://tle.oscarwatch.org/new/satellites.json";

    public const string LegacyOscarWatchTleUrl = "https://tle.oscarwatch.org/";

    public const string AmsatGpJsonUrl = "https://newark192.amsat.org/gpdata/current/daily-bulletin.json";

    public const string LegacyAmsatNasabareUrl = "https://www.amsat.org/tle/current/nasabare.txt";

    public const string CelestrakAmsatTleExampleUrl =
        "https://celestrak.org/NORAD/elements/gp.php?GROUP=amateur&FORMAT=tle";

    public const string CelestrakAmsatJsonExampleUrl =
        "https://celestrak.org/NORAD/elements/gp.php?GROUP=amateur&FORMAT=json";

    public static string GetSourceKey(TleSourceSettings settings) =>
        $"{settings.Mode}|{settings.CustomUrl?.Trim()}|{settings.LocalFilePath?.Trim()}";

    public static string GetDisplayLabel(TleSourceSettings settings) => settings.Mode switch
    {
        TleSourceMode.CustomUrl when !string.IsNullOrWhiteSpace(settings.CustomUrl) =>
            settings.CustomUrl.Trim(),
        TleSourceMode.LocalFile when !string.IsNullOrWhiteSpace(settings.LocalFilePath) =>
            Path.GetFileName(settings.LocalFilePath.Trim()),
        TleSourceMode.LocalFile => "local file",
        TleSourceMode.AmsatOrg => "amsat.org",
        _ => "tle.oscarwatch.org"
    };

    public static bool UsesNetwork(TleSourceSettings settings) =>
        settings.Mode is TleSourceMode.OscarWatch
            or TleSourceMode.AmsatOrg
            or TleSourceMode.CustomUrl;

    public static string? TryGetNetworkUrl(TleSourceSettings settings)
    {
        if (!UsesNetwork(settings))
            return null;

        return settings.Mode switch
        {
            TleSourceMode.AmsatOrg => AmsatGpJsonUrl,
            TleSourceMode.CustomUrl => string.IsNullOrWhiteSpace(settings.CustomUrl)
                ? null
                : settings.CustomUrl.Trim(),
            _ => TleService.DefaultTleUrl
        };
    }

    public static bool IsLegacyBuiltInUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var trimmed = url.Trim().TrimEnd('/');
        return string.Equals(trimmed, LegacyOscarWatchTleUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, LegacyAmsatNasabareUrl, StringComparison.OrdinalIgnoreCase);
    }

    public static TleSourceSettings NormalizeLegacyCustomUrl(TleSourceSettings settings)
    {
        if (settings.Mode != TleSourceMode.CustomUrl || string.IsNullOrWhiteSpace(settings.CustomUrl))
            return settings;

        var trimmed = settings.CustomUrl.Trim();
        if (string.Equals(trimmed.TrimEnd('/'), LegacyOscarWatchTleUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            return new TleSourceSettings
            {
                Mode = TleSourceMode.OscarWatch,
                CustomUrl = "",
                LocalFilePath = settings.LocalFilePath
            };
        }

        if (string.Equals(trimmed, LegacyAmsatNasabareUrl, StringComparison.OrdinalIgnoreCase))
        {
            return new TleSourceSettings
            {
                Mode = TleSourceMode.AmsatOrg,
                CustomUrl = "",
                LocalFilePath = settings.LocalFilePath
            };
        }

        return settings;
    }

    public static string? TryGetLocalFilePath(TleSourceSettings settings) =>
        settings.Mode == TleSourceMode.LocalFile && !string.IsNullOrWhiteSpace(settings.LocalFilePath)
            ? settings.LocalFilePath.Trim()
            : null;
}
