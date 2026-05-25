using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OscarWatch.Views;

public partial class RotatorManualWindow : Window
{
    public RotatorManualWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
