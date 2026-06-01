using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OscarWatch.ViewModels;

namespace OscarWatch;

public partial class MainWindow : Window
{
    private static readonly TimeSpan PassListScrollResetIdle = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PassListScrollResetCheckInterval = TimeSpan.FromSeconds(30);
    private const double PassListTopEpsilon = 1.0;

    private ScrollViewer? _passListScrollViewer;
    private DispatcherTimer? _passListScrollResetTimer;
    private DateTime _passListLastUserScrollUtc = DateTime.MinValue;
    private bool _passListIsScrolledDown;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
        PassesListBox.ContainerPrepared += OnPassListContainerPrepared;
        PassesListBox.ContainerClearing += OnPassListContainerClearing;
        PassesListBox.AttachedToVisualTree += (_, _) => TryAttachPassListScrollViewer();
        Closing += OnClosing;

        _passListScrollResetTimer = new DispatcherTimer { Interval = PassListScrollResetCheckInterval };
        _passListScrollResetTimer.Tick += OnPassListScrollResetTick;
        _passListScrollResetTimer.Start();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _passListScrollResetTimer?.Stop();
        DetachPassListScrollViewer();

        if (DataContext is not MainViewModel vm)
            return;

        if (vm.PrepareForShutdown())
            await vm.SaveSettingsAsync();
    }

    private void TryAttachPassListScrollViewer()
    {
        if (_passListScrollViewer is not null)
            return;

        _passListScrollViewer = PassesListBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (_passListScrollViewer is null)
            return;

        _passListScrollViewer.ScrollChanged += OnPassListScrollChanged;
        UpdatePassListScrollState(_passListScrollViewer.Offset.Y);
    }

    private void DetachPassListScrollViewer()
    {
        if (_passListScrollViewer is null)
            return;

        _passListScrollViewer.ScrollChanged -= OnPassListScrollChanged;
        _passListScrollViewer = null;
    }

    private void OnPassListScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer)
            UpdatePassListScrollState(viewer.Offset.Y);
    }

    private void UpdatePassListScrollState(double offsetY)
    {
        _passListIsScrolledDown = offsetY > PassListTopEpsilon;
        if (_passListIsScrolledDown)
            _passListLastUserScrollUtc = DateTime.UtcNow;
        else
            _passListLastUserScrollUtc = DateTime.MinValue;
    }

    private void OnPassListScrollResetTick(object? sender, EventArgs e)
    {
        if (!_passListIsScrolledDown || _passListScrollViewer is null)
            return;

        if (_passListLastUserScrollUtc == DateTime.MinValue)
            return;

        if (DateTime.UtcNow - _passListLastUserScrollUtc < PassListScrollResetIdle)
            return;

        var offset = _passListScrollViewer.Offset;
        _passListScrollViewer.Offset = new Vector(offset.X, 0);
        _passListIsScrolledDown = false;
        _passListLastUserScrollUtc = DateTime.MinValue;
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
        if (e.Key == Key.W && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (DataContext is MainViewModel vm && vm.Frequencies.ShowOperatingStyleRow)
            {
                vm.Frequencies.ToggleCwUplinkCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.None)
        {
            if (IsTextEntryFocused(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement()))
                return;

            if (DataContext is MainViewModel vm)
            {
                vm.ToggleSoloFocusedSatelliteCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (e.Key is not (Key.Add or Key.Subtract))
            return;

        if (IsTextEntryFocused(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement()))
            return;

        if (DataContext is not MainViewModel main || !main.Frequencies.HasTransponderData)
            return;

        const int stepHz = 10;
        main.Frequencies.AdjustReceiveOffsetHz(e.Key == Key.Add ? stepHz : -stepHz);
        e.Handled = true;
    }

    private static bool IsTextEntryFocused(IInputElement? focused)
    {
        for (var current = focused as Control; current is not null; current = current.Parent as Control)
        {
            if (current is TextBox or NumericUpDown)
                return true;
        }

        return false;
    }
}
