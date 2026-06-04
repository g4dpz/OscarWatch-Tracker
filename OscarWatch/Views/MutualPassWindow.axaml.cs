using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class MutualPassWindow : Window
{
    public MutualPassWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnPassesDoubleTapped(object? sender, TappedEventArgs e)
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
