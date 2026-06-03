using System.Globalization;
using OscarWatch.Localization;

namespace OscarWatch.Tests;

/// <summary>Restores all thread/default UI cultures after localization tests.</summary>
internal sealed class TestUiCulture : IDisposable
{
    private readonly CultureInfo _previousUi;
    private readonly CultureInfo _previous;
    private readonly CultureInfo? _previousDefaultUi;
    private readonly CultureInfo? _previousDefault;

    private TestUiCulture(
        CultureInfo previousUi,
        CultureInfo previous,
        CultureInfo? previousDefaultUi,
        CultureInfo? previousDefault)
    {
        _previousUi = previousUi;
        _previous = previous;
        _previousDefaultUi = previousDefaultUi;
        _previousDefault = previousDefault;
    }

    public static TestUiCulture Apply(string uiLanguage = LocalizationCulture.DefaultLanguage)
    {
        var previousUi = CultureInfo.CurrentUICulture;
        var previous = CultureInfo.CurrentCulture;
        var previousDefaultUi = CultureInfo.DefaultThreadCurrentUICulture;
        var previousDefault = CultureInfo.DefaultThreadCurrentCulture;
        LocalizationCulture.Apply(uiLanguage);
        return new TestUiCulture(previousUi, previous, previousDefaultUi, previousDefault);
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _previousUi;
        CultureInfo.CurrentCulture = _previous;
        CultureInfo.DefaultThreadCurrentUICulture = _previousDefaultUi;
        CultureInfo.DefaultThreadCurrentCulture = _previousDefault;
    }
}
