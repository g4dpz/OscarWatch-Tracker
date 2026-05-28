using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OscarWatch.Views;

public partial class AboutWindow : Window
{
    private const string GitHubRepoUrl = "https://github.com/magicbug/OscarWatch-Tracker";
    private const string GitHubSupportersUrl = "https://github.com/magicbug/OscarWatch-Tracker/blob/main/supporters.md";
    private const string GitHubSponsorsUrl = "https://github.com/sponsors/magicbug";
    private const string PayPalUrl = "https://www.paypal.com/paypalme/PGoodhall";

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

    private void OnGitHubRepoClick(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            OpenUrl(GitHubRepoUrl);
    }

    private void OnGitHubSponsorsClick(object? sender, RoutedEventArgs e) =>
        OpenUrl(GitHubSponsorsUrl);

    private void OnGitHubContributorsClick(object? sender, RoutedEventArgs e) =>
        OpenUrl(GitHubSupportersUrl);

    private void OnPayPalClick(object? sender, RoutedEventArgs e) =>
        OpenUrl(PayPalUrl);

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
