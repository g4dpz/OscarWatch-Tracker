using CommunityToolkit.Mvvm.ComponentModel;
using OscarWatch.Core.Services;
using OscarWatch.Localization;

namespace OscarWatch.ViewModels;

public sealed partial class SatelliteStatusReportViewModel : ObservableObject
{
    public SatelliteStatusReportViewModel(string satelliteName, string modeType, ILocalizationService localization)
    {
        SatelliteLabel = localization.Get("SatStatus.Report.Satellite", satelliteName);
        ModeLabel = localization.Get("SatStatus.Report.Mode", modeType);
        IsOnSelected = true;
    }

    public string SatelliteLabel { get; }
    public string ModeLabel { get; }

    [ObservableProperty]
    private bool _isOnSelected;

    [ObservableProperty]
    private bool _isOffSelected;

    [ObservableProperty]
    private bool _isTelemetryOnlySelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = "";

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public SatelliteStatusValue SelectedStatus
    {
        get
        {
            if (IsOffSelected)
                return SatelliteStatusValue.Off;
            if (IsTelemetryOnlySelected)
                return SatelliteStatusValue.TelemetryOnly;
            return SatelliteStatusValue.On;
        }
    }

    partial void OnIsOnSelectedChanged(bool value)
    {
        if (value)
        {
            IsOffSelected = false;
            IsTelemetryOnlySelected = false;
        }
    }

    partial void OnIsOffSelectedChanged(bool value)
    {
        if (value)
        {
            IsOnSelected = false;
            IsTelemetryOnlySelected = false;
        }
    }

    partial void OnIsTelemetryOnlySelectedChanged(bool value)
    {
        if (value)
        {
            IsOnSelected = false;
            IsOffSelected = false;
        }
    }
}
