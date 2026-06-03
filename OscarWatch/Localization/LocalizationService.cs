using System.Globalization;
using System.Resources;
using OscarWatch.Resources;

namespace OscarWatch.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly ResourceManager ResourceManager =
        new("OscarWatch.Resources.Strings", typeof(Strings).Assembly);

    public static LocalizationService Instance { get; } = new();

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        var value = ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
            ?? ResourceManager.GetString(key, CultureInfo.InvariantCulture);

        return string.IsNullOrEmpty(value) ? key : value;
    }

    public string Get(string key, params object[] args)
    {
        var format = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }
}
