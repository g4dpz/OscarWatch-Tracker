using Avalonia.Controls;
using Avalonia.Interactivity;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class SatelliteDatabaseMergeWindow : Window
{
    public SatelliteDatabaseMergeWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SatelliteDatabaseMergeViewModel vm)
            return;

        if (!vm.HasSelectedChanges())
        {
            Close(false);
            return;
        }

        Close(true);
    }
}
