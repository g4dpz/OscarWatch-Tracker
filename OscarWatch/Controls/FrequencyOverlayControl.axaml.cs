using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using OscarWatch.ViewModels;

namespace OscarWatch.Controls;

public partial class FrequencyOverlayControl : UserControl
{
    private const double EdgeMarginPx = 8;
    private bool _isDragging;
    private Point _dragStartPointer;
    private double _dragStartX;
    private double _dragStartY;
    private FrequencyOverlayViewModel? _subscribedVm;

    public FrequencyOverlayControl()
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

        LayoutUpdated += OnLayoutUpdated;
        SubscribeViewModel(DataContext as FrequencyOverlayViewModel);
        if (Parent is Control host)
            host.SizeChanged += OnHostSizeChanged;
        EnsureVisiblePosition();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        if (Parent is Control host)
            host.SizeChanged -= OnHostSizeChanged;
        SubscribeViewModel(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SubscribeViewModel(DataContext as FrequencyOverlayViewModel);
        EnsureVisiblePosition();
    }

    private void SubscribeViewModel(FrequencyOverlayViewModel? vm)
    {
        if (_subscribedVm == vm)
            return;

        if (_subscribedVm is not null)
            _subscribedVm.OverlayLayoutChanged -= OnOverlayLayoutChanged;

        _subscribedVm = vm;
        if (vm is not null)
            vm.OverlayLayoutChanged += OnOverlayLayoutChanged;
    }

    private void OnHostSizeChanged(object? sender, SizeChangedEventArgs e) =>
        EnsureVisiblePosition();

    private void OnOverlayLayoutChanged(object? sender, EventArgs e) =>
        EnsureVisiblePosition();

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        EnsureVisiblePosition();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) =>
        EnsureVisiblePosition();

    private Control? GetMapHost() => Parent as Control;

    private Size MeasureOverlaySize()
    {
        if (Bounds.Width > 0 && Bounds.Height > 0)
            return Bounds.Size;

        var constraint = new Size(
            Math.Max(MinWidth, 380),
            double.PositiveInfinity);
        Measure(constraint);
        return new Size(
            Math.Max(Bounds.Width, DesiredSize.Width),
            Math.Max(Bounds.Height, DesiredSize.Height));
    }

    private void EnsureVisiblePosition()
    {
        if (DataContext is not FrequencyOverlayViewModel vm)
            return;

        var host = GetMapHost();
        if (host is null || host.Bounds.Width <= 0 || host.Bounds.Height <= 0)
            return;

        var overlay = MeasureOverlaySize();
        vm.EnsureOverlayWithinHost(
            host.Bounds.Width,
            host.Bounds.Height,
            overlay.Width,
            overlay.Height);
    }

    private void OnDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not FrequencyOverlayViewModel vm)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var host = GetMapHost();
        if (host is null)
            return;

        _isDragging = true;
        _dragStartPointer = e.GetPosition(host);
        _dragStartX = vm.OverlayX;
        _dragStartY = vm.OverlayY;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || DataContext is not FrequencyOverlayViewModel vm)
            return;

        var host = GetMapHost();
        if (host is null)
            return;

        var pos = e.GetPosition(host);
        var delta = pos - _dragStartPointer;
        var overlay = MeasureOverlaySize();
        var (newX, newY) = ClampPosition(
            _dragStartX + delta.X,
            _dragStartY + delta.Y,
            host.Bounds.Width,
            host.Bounds.Height,
            overlay.Width,
            overlay.Height);
        vm.SetOverlayPosition(newX, newY);
        e.Handled = true;
    }

    private void OnOverlayPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        e.Pointer.Capture(null);

        if (DataContext is FrequencyOverlayViewModel vm)
        {
            EnsureVisiblePosition();
            vm.PersistOverlayPosition();
        }
    }

    private void OnOverlayPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) =>
        _isDragging = false;

    private void OnOffsetSpinValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (DataContext is not FrequencyOverlayViewModel vm || sender is not NumericUpDown spin)
            return;

        var khz = (double)(spin.Value ?? 0m);
        if (Math.Abs(vm.ReceiveOffsetKHz - khz) < 0.0001)
            return;
        vm.ReceiveOffsetKHz = khz;
        // Property change handler calls ApplyOffsetEdit (display now, CAT debounced in MainViewModel).
    }

    private void OnOffsetStepClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FrequencyOverlayViewModel vm || sender is not Button btn)
            return;

        if (btn.Tag is string tag && int.TryParse(tag, out var deltaHz))
            vm.AdjustReceiveOffsetHz(deltaHz);
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
