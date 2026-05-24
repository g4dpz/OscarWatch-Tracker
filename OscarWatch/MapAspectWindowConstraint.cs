using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace OscarWatch;

/// <summary>
/// Keeps the main window client area sized so the world map column stays 2:1 (equirectangular).
/// </summary>
internal sealed class MapAspectWindowConstraint
{
    public const double MapAspectRatio = 2.0;
    public const double SidebarWidth = 320;
    public const double MinMapWidth = 400;
    public const double MinMapHeight = 200;

    private const double DefaultVerticalChrome = 56;

    private readonly Window _window;
    private double _verticalChrome = DefaultVerticalChrome;
    private double _lastClientWidth;
    private double _lastClientHeight;
    private double _frameWidth;
    private double _frameHeight;
    private bool _adjusting;
    private bool _tracking;
    private bool _chromeAspectApplied;

    public MapAspectWindowConstraint(Window window)
    {
        _window = window;
        _window.Opened += OnOpened;
        _window.Resized += OnResized;
        _window.PropertyChanged += OnWindowPropertyChanged;
        _window.Closed += (_, _) => _tracking = false;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_tracking || _adjusting || e.Property != Window.WindowStateProperty)
            return;

        // Maximize/restore changes client area before chrome metrics settle; re-apply after layout.
        Dispatcher.UIThread.Post(
            () => EnforceAspect(widthDriven: null),
            DispatcherPriority.Loaded);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _window.LayoutUpdated += OnLayoutUpdated;
        RefreshChrome();
        RefreshFrameChrome();
        ApplyMinimumWindowSize();
        _lastClientWidth = _window.ClientSize.Width;
        _lastClientHeight = _window.ClientSize.Height;
        _tracking = true;
        EnforceAspect(widthDriven: null);
    }

    private void RefreshFrameChrome()
    {
        _frameWidth = Math.Max(0, _window.Width - _window.ClientSize.Width);
        _frameHeight = Math.Max(0, _window.Height - _window.ClientSize.Height);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_chromeAspectApplied || !_tracking || _adjusting)
            return;

        var previousChrome = _verticalChrome;
        RefreshChrome();

        // Menu/status bar heights are often 0 on first layout; re-enforce once when chrome is real.
        if (previousChrome <= DefaultVerticalChrome + 0.5
            && _verticalChrome > DefaultVerticalChrome + 0.5)
        {
            _chromeAspectApplied = true;
            EnforceAspect(widthDriven: null);
        }
    }

    private void OnResized(object? sender, WindowResizedEventArgs e)
    {
        if (!_tracking || _adjusting)
            return;

        var cs = _window.ClientSize;
        var dw = Math.Abs(cs.Width - _lastClientWidth);
        var dh = Math.Abs(cs.Height - _lastClientHeight);
        _lastClientWidth = cs.Width;
        _lastClientHeight = cs.Height;

        if (dw < 0.5 && dh < 0.5)
            return;

        EnforceAspect(dw >= dh);
    }

    private void RefreshChrome()
    {
        if (_window.Content is not DockPanel dock)
            return;

        var vertical = 0.0;
        foreach (var child in dock.Children)
        {
            if (child is Menu && DockPanel.GetDock(child) == Dock.Top)
                vertical += child.Bounds.Height > 0 ? child.Bounds.Height : 24;
            else if (child is Border && DockPanel.GetDock(child) == Dock.Bottom)
                vertical += child.Bounds.Height > 0 ? child.Bounds.Height : 32;
        }

        if (vertical > 0)
            _verticalChrome = vertical;
    }

    private void ApplyMinimumWindowSize()
    {
        _window.MinWidth = MinMapWidth + SidebarWidth;
        _window.MinHeight = MinMapHeight + _verticalChrome;
    }

    private void EnforceAspect(bool? widthDriven)
    {
        RefreshChrome();
        RefreshFrameChrome();
        ApplyMinimumWindowSize();

        var cs = _window.ClientSize;
        var mapW = cs.Width - SidebarWidth;
        var mapH = cs.Height - _verticalChrome;
        if (mapW <= 0 || mapH <= 0)
            return;

        double targetMapW;
        double targetMapH;

        if (widthDriven == true)
        {
            targetMapW = Math.Max(mapW, MinMapWidth);
            targetMapH = targetMapW / MapAspectRatio;
        }
        else if (widthDriven == false)
        {
            targetMapH = Math.Max(mapH, MinMapHeight);
            targetMapW = targetMapH * MapAspectRatio;
        }
        else
        {
            targetMapH = Math.Max(mapH, MinMapHeight);
            targetMapW = targetMapH * MapAspectRatio;
            if (targetMapW < MinMapWidth)
            {
                targetMapW = MinMapWidth;
                targetMapH = targetMapW / MapAspectRatio;
            }
        }

        targetMapH = Math.Max(targetMapH, MinMapHeight);
        targetMapW = Math.Max(targetMapW, MinMapWidth);

        var newClientW = targetMapW + SidebarWidth;
        var newClientH = targetMapH + _verticalChrome;

        if (Math.Abs(newClientW - cs.Width) < 0.5 && Math.Abs(newClientH - cs.Height) < 0.5)
            return;

        _adjusting = true;
        try
        {
            _window.Width = newClientW + _frameWidth;
            _window.Height = newClientH + _frameHeight;
            _lastClientWidth = newClientW;
            _lastClientHeight = newClientH;
        }
        finally
        {
            _adjusting = false;
        }
    }
}
