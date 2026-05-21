using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OscarWatch.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {GetVersionText()}";
    }

    private static string GetVersionText()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational;

        var version = assembly.GetName().Version;
        return version is null ? "dev" : version.ToString(3);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
