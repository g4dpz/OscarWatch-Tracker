using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OscarWatch.Controls;
using OscarWatch.Localization;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class DopplerPassInsightsWindow : Window
{
    private DopplerTimeSeriesChartControl? _chartControl;

    public DopplerPassInsightsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnOpenCsvClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Instance.Get("DopplerInsights.Picker.Title"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CSV files") { Patterns = ["*.csv"] }
            ]
        });

        if (file.Count == 0)
            return;

        if (DataContext is DopplerPassInsightsViewModel vm)
        {
            await vm.LoadFromFileAsync(file[0].Path.LocalPath).ConfigureAwait(true);
            ResetChartView();
        }
    }

    private void ResetChartView()
    {
        _chartControl ??= this.FindControl<DopplerTimeSeriesChartControl>("ChartControl");
        if (_chartControl != null)
            _chartControl.ResetView();
    }

    private async void OnOpenCompareCsvClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Instance.Get("DopplerInsights.Picker.CompareTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CSV files") { Patterns = ["*.csv"] }
            ]
        });

        if (file.Count == 0)
            return;

        if (DataContext is DopplerPassInsightsViewModel vm)
            await vm.LoadComparisonFromFileAsync(file[0].Path.LocalPath).ConfigureAwait(true);
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => ZoomChart(2.0);

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => ZoomChart(0.5);

    private void OnZoomResetClick(object? sender, RoutedEventArgs e)
    {
        _chartControl ??= this.FindControl<DopplerTimeSeriesChartControl>("ChartControl");
        if (_chartControl != null)
            _chartControl.ResetView();
    }

    private void ZoomChart(double factor)
    {
        _chartControl ??= this.FindControl<DopplerTimeSeriesChartControl>("ChartControl");
        if (_chartControl != null)
            _chartControl.ZoomLevel *= factor;
    }

    private void OnChartControlLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is DopplerTimeSeriesChartControl chart)
            _chartControl = chart;
    }
}