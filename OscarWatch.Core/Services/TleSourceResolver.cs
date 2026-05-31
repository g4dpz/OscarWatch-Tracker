using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public static class TleSourceResolver
{
    public const string CelestrakAmsatExampleUrl =
        "https://celestrak.org/NORAD/elements/gp.php?GROUP=amateur&FORMAT=tle";

    public static string GetSourceKey(TleSourceSettings settings) =>
        $"{settings.Mode}|{settings.CustomUrl?.Trim()}|{settings.LocalFilePath?.Trim()}";

    public static string GetDisplayLabel(TleSourceSettings settings) => settings.Mode switch
    {
        TleSourceMode.CustomUrl when !string.IsNullOrWhiteSpace(settings.CustomUrl) =>
            settings.CustomUrl.Trim(),
        TleSourceMode.LocalFile when !string.IsNullOrWhiteSpace(settings.LocalFilePath) =>
            Path.GetFileName(settings.LocalFilePath.Trim()),
        TleSourceMode.LocalFile => "local file",
        _ => "tle.oscarwatch.org"
    };

    public static bool UsesNetwork(TleSourceSettings settings) =>
        settings.Mode is TleSourceMode.OscarWatch or TleSourceMode.CustomUrl;

    public static string? TryGetNetworkUrl(TleSourceSettings settings)
    {
        if (!UsesNetwork(settings))
            return null;

        return settings.Mode switch
        {
            TleSourceMode.CustomUrl => string.IsNullOrWhiteSpace(settings.CustomUrl)
                ? null
                : settings.CustomUrl.Trim(),
            _ => TleService.DefaultTleUrl
        };
    }

    public static string? TryGetLocalFilePath(TleSourceSettings settings) =>
        settings.Mode == TleSourceMode.LocalFile && !string.IsNullOrWhiteSpace(settings.LocalFilePath)
            ? settings.LocalFilePath.Trim()
            : null;
}
