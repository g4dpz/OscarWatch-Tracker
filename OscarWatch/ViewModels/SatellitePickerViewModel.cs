using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Services;

namespace OscarWatch.ViewModels;

public partial class SatellitePickerViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ITleService _tleService;

    [ObservableProperty]
    private string _searchText = "";

    public ObservableCollection<SatelliteItemViewModel> Satellites { get; } = [];

    public SatellitePickerViewModel(ISettingsService settings, ITleService tleService)
    {
        _settings = settings;
        _tleService = tleService;
        Load();
    }

    private void Load()
    {
        Satellites.Clear();
        var enabled = new HashSet<string>(_settings.Current.EnabledSatelliteNames, StringComparer.OrdinalIgnoreCase);
        foreach (var sat in _tleService.Catalog.OrderBy(s => s.Name))
        {
            Satellites.Add(new SatelliteItemViewModel
            {
                Name = sat.Name,
                NoradId = sat.NoradId,
                IsEnabled = enabled.Contains(sat.Name),
                IsStale = sat.IsStale(TimeSpan.FromDays(14)),
                EpochText = sat.EpochUtc?.ToString("yyyy-MM-dd") ?? "?"
            });
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        var filter = value.Trim();
        foreach (var s in Satellites)
        {
            s.IsVisible = string.IsNullOrEmpty(filter) ||
                          s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var s in Satellites.Where(x => x.IsVisible))
            s.IsEnabled = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var s in Satellites)
            s.IsEnabled = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _settings.Current.EnabledSatelliteNames = Satellites
            .Where(s => s.IsEnabled)
            .Select(s => s.Name)
            .ToList();
        await _settings.SaveAsync();
    }
}

public partial class SatelliteItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isVisible = true;

    public string Name { get; init; } = "";
    public string NoradId { get; init; } = "";
    public bool IsStale { get; init; }
    public string EpochText { get; init; } = "";
}
