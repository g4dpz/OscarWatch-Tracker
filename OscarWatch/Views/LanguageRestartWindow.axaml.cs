using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OscarWatch.Views;

public partial class LanguageRestartWindow : Window
{
    public LanguageRestartWindow()
    {
        InitializeComponent();
    }

    public static async Task<bool> ShowAsync(Window owner)
    {
        var window = new LanguageRestartWindow();
        return await window.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnLaterClick(object? sender, RoutedEventArgs e) => Close(false);
}
