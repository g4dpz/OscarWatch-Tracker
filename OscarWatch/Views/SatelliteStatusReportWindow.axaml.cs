using Avalonia.Controls;
using Avalonia.Interactivity;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class SatelliteStatusReportWindow : Window
{
    private readonly SatelliteStatusReportViewModel _vm;

    public SatelliteStatusReportWindow()
        : this("—", "—", LocalizationService.Instance)
    {
    }

    public SatelliteStatusReportWindow(string satelliteName, string modeType, ILocalizationService localization)
    {
        InitializeComponent();
        _vm = new SatelliteStatusReportViewModel(satelliteName, modeType, localization);
        DataContext = _vm;
    }

    public SatelliteStatusValue SelectedStatus => _vm.SelectedStatus;

    private void OnSubmitClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
