using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OscarWatch.Core.Ft4;
using OscarWatch.Localization;

namespace OscarWatch.Views;

/// <summary>Asks for the callsign or grid to pounce on. Closes with the parsed target, or null on Cancel.</summary>
public partial class Ft4PounceWindow : Window
{
    public Ft4PounceWindow() : this("")
    {
    }

    public Ft4PounceWindow(string initialText)
    {
        InitializeComponent();
        TargetTextBox.Text = initialText;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TargetTextBox.Focus();
            TargetTextBox.SelectAll();
        });
    }

    private void OnArmClick(object? sender, RoutedEventArgs e)
    {
        var text = TargetTextBox.Text ?? "";
        if (!Ft4PounceTarget.TryParse(text, out var target))
        {
            ErrorText.Text = LocalizationService.Instance.Get("Ft4.Status.PounceInvalid", text.Trim());
            ErrorText.IsVisible = true;
            TargetTextBox.Focus();
            return;
        }

        Close(target);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
