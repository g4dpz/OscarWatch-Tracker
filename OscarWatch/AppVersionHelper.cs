using System.Reflection;
using OscarWatch.Core.Services;

namespace OscarWatch;

internal static class AppVersionHelper
{
    public static Version? GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var parsed = ReleaseVersion.TryParseVersionString(informational);
            if (parsed is not null)
                return parsed;
        }

        var version = assembly.GetName().Version;
        return version is null ? null : new Version(version.Major, version.Minor, version.Build);
    }

    public static string GetDisplayVersionText()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational;

        var version = assembly.GetName().Version;
        return version is null ? "dev" : version.ToString(3);
    }
}
