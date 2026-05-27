using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class AddSatelliteFromTleWindow : Window
{
    public AddSatelliteFromTleWindow()
    {
        InitializeComponent();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => ConfirmAndClose();

    private void OnCandidateDoubleTapped(object? sender, TappedEventArgs e) => ConfirmAndClose();

    private void ConfirmAndClose()
    {
        if (DataContext is not AddSatelliteFromTleViewModel vm)
        {
            Close(null);
            return;
        }

        if (!vm.TryConfirm(out var name, out _))
            return;

        Close(name);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
