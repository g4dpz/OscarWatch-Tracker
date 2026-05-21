using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using OscarWatch.ViewModels;

namespace OscarWatch;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PassesListBox.ContainerPrepared += OnPassListContainerPrepared;
        _ = new MapAspectWindowConstraint(this);
    }

    private static void OnPassListContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem item)
            return;

        if (item.DataContext is PassDayHeaderViewModel)
        {
            item.IsEnabled = false;
            item.Focusable = false;
            item.Classes.Add("pass-day-header");
        }
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainViewModel vm)
            await vm.InitializeAsync();
    }
}
