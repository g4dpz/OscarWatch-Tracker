using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OscarWatch.Core.Models;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class CreateLogbookWindow : Window
{
    public CreateLogbookWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await Dispatcher.UIThread.InvokeAsync(() => NameTextBox?.Focus());
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CreateLogbookViewModel vm)
        {
            Close(null);
            return;
        }

        if (!vm.TryConfirm(out var request))
            return;

        Close(request);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
