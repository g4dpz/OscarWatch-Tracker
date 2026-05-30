using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Threading;
using OscarWatch.ViewModels;

namespace OscarWatch.Controls;

public partial class DxStationOverlayControl : UserControl
{
    private const double EdgeMarginPx = 8;

    private bool _isDragging;
    private IPointer? _dragPointer;
    private Point _dragStartPointer;
    private double _dragStartX;
    private double _dragStartY;
    private DxStationOverlayViewModel? _subscribedVm;
    private Control? _mapHost;
    private Window? _hostWindow;
    private int _ensureVisibleGeneration;

    public DxStationOverlayControl()
    {
        InitializeComponent();
        PointerMoved += OnOverlayPointerMoved;
        PointerReleased += OnOverlayPointerReleased;
        PointerCaptureLost += OnOverlayPointerCaptureLost;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var handle = this.FindControl<Control>("DragHandle");
        if (handle is not null)
            handle.PointerPressed += OnDragHandlePointerPressed;

        SubscribeViewModel(DataContext as DxStationOverlayViewModel);
        BindMapHosts();
        if (_hostWindow is not null)
        {
            _hostWindow.Resized += OnHostWindowResized;
            _hostWindow.PropertyChanged += OnHostWindowPropertyChanged;
            _hostWindow.Deactivated += OnHostWindowDeactivated;
            _hostWindow.PointerReleased += OnHostWindowPointerReleased;
        }

        ScheduleEnsureVisiblePosition();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_hostWindow is not null)
        {
            _hostWindow.Resized -= OnHostWindowResized;
            _hostWindow.PropertyChanged -= OnHostWindowPropertyChanged;
            _hostWindow.Deactivated -= OnHostWindowDeactivated;
            _hostWindow.PointerReleased -= OnHostWindowPointerReleased;
        }

        EndDrag();
        UnbindMapHosts();
        _hostWindow = null;
        SubscribeViewModel(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SubscribeViewModel(DataContext as DxStationOverlayViewModel);
        ScheduleEnsureVisiblePosition();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ScheduleEnsureVisiblePosition();
    }

    private void BindMapHosts()
    {
        UnbindMapHosts();
        _mapHost = Parent as Control;
        if (_mapHost is not null)
            _mapHost.SizeChanged += OnMapHostSizeChanged;

        _hostWindow = TopLevel.GetTopLevel(this) as Window;
    }

    private void UnbindMapHosts()
    {
        if (_mapHost is not null)
            _mapHost.SizeChanged -= OnMapHostSizeChanged;
        _mapHost = null;
    }

    private void OnHostWindowResized(object? sender, WindowResizedEventArgs e) =>
        ScheduleEnsureVisiblePosition();

    private void OnHostWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
            ScheduleEnsureVisiblePosition();
    }

    private void OnMapHostSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ScheduleEnsureVisiblePosition();

    private void SubscribeViewModel(DxStationOverlayViewModel? vm)
    {
        if (_subscribedVm == vm)
            return;

        if (_subscribedVm is not null)
        {
            _subscribedVm.OverlayLayoutChanged -= OnOverlayLayoutChanged;
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedVm = vm;
        if (vm is not null)
        {
            vm.OverlayLayoutChanged += OnOverlayLayoutChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DxStationOverlayViewModel.IsCollapsed)
            or nameof(DxStationOverlayViewModel.OverlayMinWidth)
            or nameof(DxStationOverlayViewModel.OverlayMaxWidth)
            or nameof(DxStationOverlayViewModel.IsActive))
            ScheduleEnsureVisiblePosition();
    }

    private void OnOverlayLayoutChanged(object? sender, EventArgs e) =>
        ScheduleEnsureVisiblePosition();

    private void ScheduleEnsureVisiblePosition()
    {
        var generation = Interlocked.Increment(ref _ensureVisibleGeneration);

        void RunIfCurrent()
        {
            if (generation != _ensureVisibleGeneration)
                return;
            EnsureVisiblePosition();
        }

        Dispatcher.UIThread.Post(RunIfCurrent, DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(RunIfCurrent, DispatcherPriority.Render);
    }

    private Visual? GetCoordinateHost() => _mapHost;

    private bool TryGetHostSize(out double width, out double height)
    {
        width = 0;
        height = 0;

        if (_mapHost is not { Bounds.Width: > 0, Bounds.Height: > 0 } host)
            return false;

        width = host.Bounds.Width;
        height = host.Bounds.Height;
        return true;
    }

    private Size MeasureOverlaySizeForClamp()
    {
        if (Bounds.Width > 0 && Bounds.Height > 0)
            return Bounds.Size;

        var vm = DataContext as DxStationOverlayViewModel;
        var minW = vm?.OverlayMinWidth ?? 280;
        var collapsed = vm?.IsCollapsed == true;
        var w = DesiredSize.Width > 0 ? DesiredSize.Width : (collapsed ? 280 : 320);
        var h = DesiredSize.Height > 0 ? DesiredSize.Height : (collapsed ? 44 : 200);
        return new Size(Math.Max(w, minW), h);
    }

    private void EnsureVisiblePosition()
    {
        if (_isDragging || DataContext is not DxStationOverlayViewModel vm)
            return;

        if (!TryGetHostSize(out var hostWidth, out var hostHeight))
            return;

        var overlay = MeasureOverlaySizeForClamp();
        vm.EnsureOverlayWithinHost(
            hostWidth,
            hostHeight,
            overlay.Width,
            overlay.Height);
    }

    private void OnDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not DxStationOverlayViewModel vm)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is Visual source && CollapseToggle is not null && IsDescendantOf(source, CollapseToggle))
            return;

        var host = GetCoordinateHost();
        if (host is null)
            return;

        _isDragging = true;
        _dragPointer = e.Pointer;
        _dragStartPointer = e.GetPosition(host);
        _dragStartX = vm.OverlayX;
        _dragStartY = vm.OverlayY;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnHostWindowDeactivated(object? sender, EventArgs e) =>
        EndDrag(persistPosition: true);

    private void OnHostWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
            EndDrag(persistPosition: true);
    }

    private void OnCollapseToggleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is DxStationOverlayViewModel vm)
            vm.ToggleCollapseCommand.Execute(null);
        e.Handled = true;
    }

    private static bool IsDescendantOf(Visual? node, Visual ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;
            node = node.GetVisualParent() as Visual;
        }

        return false;
    }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || DataContext is not DxStationOverlayViewModel vm)
            return;

        if (!TryGetHostSize(out var hostWidth, out var hostHeight))
            return;

        var host = GetCoordinateHost();
        if (host is null)
            return;

        var pos = e.GetPosition(host);
        var delta = pos - _dragStartPointer;
        var overlay = MeasureOverlaySizeForClamp();
        var (newX, newY) = ClampPosition(
            _dragStartX + delta.X,
            _dragStartY + delta.Y,
            hostWidth,
            hostHeight,
            overlay.Width,
            overlay.Height);
        vm.SetOverlayPosition(newX, newY);
        e.Handled = true;
    }

    private void OnOverlayPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        EndDrag(persistPosition: true);

    private void OnOverlayPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) =>
        EndDrag();

    private void EndDrag(bool persistPosition = false)
    {
        var wasDragging = _isDragging;
        _isDragging = false;
        _dragPointer?.Capture(null);
        _dragPointer = null;

        if (!wasDragging || !persistPosition || DataContext is not DxStationOverlayViewModel vm)
            return;

        EnsureVisiblePosition();
        vm.PersistOverlayPosition();
        ScheduleEnsureVisiblePosition();
    }

    private static (double X, double Y) ClampPosition(
        double x,
        double y,
        double hostWidth,
        double hostHeight,
        double overlayWidth,
        double overlayHeight)
    {
        var maxX = Math.Max(EdgeMarginPx, hostWidth - overlayWidth - EdgeMarginPx);
        var maxY = Math.Max(EdgeMarginPx, hostHeight - overlayHeight - EdgeMarginPx);
        return (Math.Clamp(x, EdgeMarginPx, maxX), Math.Clamp(y, EdgeMarginPx, maxY));
    }
}
