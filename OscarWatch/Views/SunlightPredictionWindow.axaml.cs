using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OscarWatch.Views;

public partial class SunlightPredictionWindow : Window
{
    public SunlightPredictionWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
