using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Services;

namespace OscarWatch.ViewModels;

public partial class RotatorManualViewModel : ViewModelBase
{
    private readonly IRotatorController _rotator;
    private readonly ISettingsService _settings;
    private Action? _refreshMainDisplay;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RotateCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(ParkCommand))]
    private bool _isConnected;

    [ObservableProperty]
    private string _currentPositionText = "—";

    [ObservableProperty]
    private decimal _azimuthDeg;

    [ObservableProperty]
    private decimal _elevationDeg;

    [ObservableProperty]
    private decimal _maxAzimuthDeg = 450;

    [ObservableProperty]
    private decimal _maxElevationDeg = 180;

    public RotatorManualViewModel(IRotatorController rotator, ISettingsService settings)
    {
        _rotator = rotator;
        _settings = settings;
    }

    public void Initialize(Action refreshMainDisplay)
    {
        _refreshMainDisplay = refreshMainDisplay;
        var rotatorSettings = _settings.Current.Rotator;
        MaxAzimuthDeg = (decimal)rotatorSettings.MaxAzimuthDeg;
        MaxElevationDeg = (decimal)rotatorSettings.MaxElevationDeg;
        RefreshPosition();

        var status = _rotator.GetPositionStatus();
        if (status.AzimuthDeg is { } az)
            AzimuthDeg = az;
        else if (status.CommandedAzimuthDeg is { } commanded)
            AzimuthDeg = commanded;

        if (status.ElevationDeg is { } el)
            ElevationDeg = el;
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private void Rotate()
    {
        _rotator.MoveTo((double)AzimuthDeg, (double)ElevationDeg, _settings.Current.Rotator);
        _refreshMainDisplay?.Invoke();
        RefreshPosition();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private void Stop()
    {
        _rotator.Stop(_settings.Current.Rotator);
        RefreshPosition();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private void Park()
    {
        _rotator.Park(_settings.Current.Rotator);
        _refreshMainDisplay?.Invoke();
        RefreshPosition();
    }

    private void RefreshPosition()
    {
        var status = _rotator.GetPositionStatus();
        IsConnected = status.IsConnected;
        CurrentPositionText = !status.IsConnected
            ? "Not connected"
            : status.ElevationDeg is { } el
                ? $"Az {MainViewModel.FormatRotatorAzimuthText(status)} · El {el}°"
                : "Position unknown";
    }
}
