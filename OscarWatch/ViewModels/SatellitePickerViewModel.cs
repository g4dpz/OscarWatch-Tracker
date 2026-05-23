using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Services;

namespace OscarWatch.ViewModels;

public enum SatellitePickerSelectionFilter
{
    All,
    Selected,
    NotSelected
}

public partial class SatellitePickerViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ITleService _tleService;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private SatellitePickerFilterOption? _selectedSelectionFilterChoice;

    public IReadOnlyList<SatellitePickerFilterOption> SelectionFilterChoices { get; } =
    [
        new(SatellitePickerSelectionFilter.All, "All"),
        new(SatellitePickerSelectionFilter.Selected, "Selected"),
        new(SatellitePickerSelectionFilter.NotSelected, "Not Selected")
    ];

    public ObservableCollection<SatelliteItemViewModel> Satellites { get; } = [];

    public SatellitePickerViewModel(ISettingsService settings, ITleService tleService)
    {
        _settings = settings;
        _tleService = tleService;
        SelectedSelectionFilterChoice = SelectionFilterChoices[0];
        Load();
    }

    private void Load()
    {
        Satellites.Clear();
        var enabled = new HashSet<string>(_settings.Current.EnabledSatelliteNames, StringComparer.OrdinalIgnoreCase);
        foreach (var sat in _tleService.Catalog.OrderBy(s => s.Name))
        {
            var item = new SatelliteItemViewModel
            {
                Name = sat.Name,
                NoradId = sat.NoradId,
                IsEnabled = enabled.Contains(sat.Name),
                IsStale = sat.IsStale(TimeSpan.FromDays(14)),
                EpochText = sat.EpochUtc?.ToString("yyyy-MM-dd") ?? "?"
            };
            item.EnabledChanged += ApplyFilters;
            Satellites.Add(item);
        }

        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedSelectionFilterChoiceChanged(SatellitePickerFilterOption? value) => ApplyFilters();

    private void ApplyFilters()
    {
        var search = SearchText.Trim();
        var selectionFilter = SelectedSelectionFilterChoice?.Value ?? SatellitePickerSelectionFilter.All;

        foreach (var s in Satellites)
        {
            var matchesSearch = string.IsNullOrEmpty(search) ||
                                s.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
            var matchesSelection = selectionFilter switch
            {
                SatellitePickerSelectionFilter.Selected => s.IsEnabled,
                SatellitePickerSelectionFilter.NotSelected => !s.IsEnabled,
                _ => true
            };
            s.IsVisible = matchesSearch && matchesSelection;
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

    public event Action? EnabledChanged;

    partial void OnIsEnabledChanged(bool value) => EnabledChanged?.Invoke();
}

public sealed record SatellitePickerFilterOption(SatellitePickerSelectionFilter Value, string Label);
