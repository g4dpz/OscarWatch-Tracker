using Avalonia.Markup.Xaml;

namespace OscarWatch.Localization;

/// <summary>XAML: <c>{local:Localize Key=Menu.File}</c></summary>
public sealed class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        LocalizationService.Instance.Get(Key);
}
