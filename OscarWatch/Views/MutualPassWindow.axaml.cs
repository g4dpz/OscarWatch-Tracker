using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class MutualPassWindow : Window
{
    public MutualPassWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnPassesPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(PassesGrid).Properties.IsRightButtonPressed)
            return;

        for (var node = e.Source as Visual; node is not null; node = node.GetVisualParent() as Visual)
        {
            if (node is DataGridRow row && row.DataContext is MutualPassRow passRow)
            {
                PassesGrid.SelectedItem = passRow;
                break;
            }
        }
    }

    private async void OnCopyPassClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MutualPassViewModel vm)
            return;

        if (PassesGrid.SelectedItem is not MutualPassRow row)
            return;

        var text = vm.FormatCopyText(row);
        if (string.IsNullOrEmpty(text))
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    private void OnOpenVisualizerClick(object? sender, RoutedEventArgs e) =>
        OpenVisualizerForSelectedPass();

    private void OnPassesDoubleTapped(object? sender, TappedEventArgs e) =>
        OpenVisualizerForSelectedPass();

    private void OpenVisualizerForSelectedPass()
    {
        if (DataContext is not MutualPassViewModel vm)
            return;

        if (PassesGrid.SelectedItem is not MutualPassRow row)
            return;

        var visualizerVm = vm.CreateVisualizerViewModel(row);
        if (visualizerVm is null)
            return;

        new MutualPassVisualizerWindow
        {
            DataContext = visualizerVm
        }.Show(this);
    }
}
