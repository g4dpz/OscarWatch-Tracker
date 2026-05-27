using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using OscarWatch.ViewModels;

namespace OscarWatch;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
        PassesListBox.ContainerPrepared += OnPassListContainerPrepared;
        PassesListBox.ContainerClearing += OnPassListContainerClearing;
        Closing += OnClosing;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (vm.PrepareForShutdown())
            await vm.SaveSettingsAsync();
    }

    private static void OnPassListContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container is ListBoxItem item)
            item.Classes.Remove("pass-day-header");
    }

    private static void OnPassListContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem item)
            return;

        var isHeader = item.DataContext is PassDayHeaderViewModel;
        item.IsEnabled = !isHeader;
        item.Focusable = !isHeader;

        if (isHeader)
            item.Classes.Add("pass-day-header");
        else
            item.Classes.Remove("pass-day-header");
    }

    private void OnSidebarLayoutSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (SidebarTopScrollViewer is null)
            return;

        const double minPassesHeight = 140;
        var maxTopHeight = e.NewSize.Height - minPassesHeight;
        SidebarTopScrollViewer.MaxHeight = maxTopHeight > 0 ? maxTopHeight : double.PositiveInfinity;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainViewModel vm)
            await vm.InitializeAsync();
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.W || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        if (DataContext is not MainViewModel vm || !vm.Frequencies.ShowOperatingStyleRow)
            return;

        vm.Frequencies.ToggleCwUplinkCommand.Execute(null);
        e.Handled = true;
    }
}
