using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.ViewModels;

public partial class SatelliteDatabaseEditorViewModel : ViewModelBase
{
    private readonly ISatelliteDatabaseEditor _editor;
    private bool _syncingModeFields;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _databasePathText = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private SatelliteRadioEntry? _selectedSatellite;

    [ObservableProperty]
    private SatelliteTransponderMode? _selectedMode;

    [ObservableProperty]
    private string _satelliteName = "";

    [ObservableProperty]
    private string _modeType = "";

    [ObservableProperty]
    private double _downlinkKHz;

    [ObservableProperty]
    private double _uplinkKHz;

    [ObservableProperty]
    private string _downlinkMode = "USB";

    [ObservableProperty]
    private string _uplinkMode = "LSB";

    [ObservableProperty]
    private string _doppler = "NOR";

    [ObservableProperty]
    private double? _ctcssHz;

    [ObservableProperty]
    private double? _ctcssArmHz;

    public ObservableCollection<SatelliteRadioEntry> Satellites { get; } = [];

    public bool HasSelectedSatellite => SelectedSatellite is not null;

    public bool HasSelectedMode => SelectedMode is not null;

    public IReadOnlyList<string> OperatingModes { get; } =
        ["USB", "LSB", "FM", "FMN", "CW", "DATA-USB"];

    public IReadOnlyList<string> DopplerOptions { get; } = ["NOR", "REV"];

    public SatelliteDatabaseEditorViewModel(ISatelliteDatabaseEditor editor)
    {
        _editor = editor;
        Load();
    }

    private void Load()
    {
        Satellites.Clear();
        foreach (var entry in _editor.LoadForEditing())
            Satellites.Add(entry);

        DatabasePathText = _editor.IsUsingUserDatabase
            ? $"User database: {_editor.UserPath}"
            : $"Shipped defaults: {_editor.BundledPath}";

        SelectedSatellite = Satellites.FirstOrDefault();
        StatusMessage = $"{Satellites.Count} satellites loaded.";
    }

    partial void OnSearchTextChanged(string value) => NotifyFilteredSatellites();

    partial void OnSelectedSatelliteChanged(SatelliteRadioEntry? value)
    {
        SatelliteName = value?.Name ?? "";
        SelectedMode = value?.Modes.FirstOrDefault();
        OnPropertyChanged(nameof(HasSelectedSatellite));
    }

    partial void OnSelectedModeChanged(SatelliteTransponderMode? value)
    {
        LoadModeFields(value);
        OnPropertyChanged(nameof(HasSelectedMode));
    }

    partial void OnSatelliteNameChanged(string value)
    {
        if (SelectedSatellite is not null)
            SelectedSatellite.Name = value.Trim();
    }

    partial void OnModeTypeChanged(string value) => ApplyModeField(m => m.Type = value.Trim());
    partial void OnDownlinkKHzChanged(double value) => ApplyModeField(m => m.DownlinkKHz = value);
    partial void OnUplinkKHzChanged(double value) => ApplyModeField(m => m.UplinkKHz = value);
    partial void OnDownlinkModeChanged(string value) => ApplyModeField(m => m.DownlinkMode = value);
    partial void OnUplinkModeChanged(string value) => ApplyModeField(m => m.UplinkMode = value);
    partial void OnDopplerChanged(string value) => ApplyModeField(m => m.Doppler = value);
    partial void OnCtcssHzChanged(double? value) => ApplyModeField(m => m.CtcssHz = value is > 0 ? value : null);
    partial void OnCtcssArmHzChanged(double? value) => ApplyModeField(m => m.CtcssArmHz = value is > 0 ? value : null);

    private void LoadModeFields(SatelliteTransponderMode? mode)
    {
        _syncingModeFields = true;
        try
        {
            if (mode is null)
            {
                ModeType = "";
                DownlinkKHz = 0;
                UplinkKHz = 0;
                DownlinkMode = "USB";
                UplinkMode = "LSB";
                Doppler = "NOR";
                CtcssHz = null;
                CtcssArmHz = null;
                return;
            }

            ModeType = mode.Type;
            DownlinkKHz = mode.DownlinkKHz;
            UplinkKHz = mode.UplinkKHz;
            DownlinkMode = mode.DownlinkMode;
            UplinkMode = mode.UplinkMode;
            Doppler = mode.Doppler;
            CtcssHz = mode.CtcssHz;
            CtcssArmHz = mode.CtcssArmHz;
        }
        finally
        {
            _syncingModeFields = false;
        }
    }

    private void ApplyModeField(Action<SatelliteTransponderMode> apply)
    {
        if (_syncingModeFields || SelectedMode is null)
            return;

        apply(SelectedMode);
    }

    private void NotifyFilteredSatellites() => OnPropertyChanged(nameof(FilteredSatellites));

    public IEnumerable<SatelliteRadioEntry> FilteredSatellites
    {
        get
        {
            var filter = SearchText.Trim();
            return string.IsNullOrEmpty(filter)
                ? Satellites
                : Satellites.Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
    }

    [RelayCommand]
    private void AddSatellite()
    {
        var entry = new SatelliteRadioEntry
        {
            Name = "New satellite",
            Modes =
            [
                new SatelliteTransponderMode
                {
                    Type = "FM",
                    DownlinkKHz = 145_825,
                    UplinkKHz = 145_825,
                    DownlinkMode = "FMN",
                    UplinkMode = "FMN",
                    Doppler = "NOR"
                }
            ]
        };
        Satellites.Add(entry);
        SelectedSatellite = entry;
        NotifyFilteredSatellites();
        StatusMessage = "Added satellite.";
    }

    [RelayCommand]
    private void RemoveSatellite()
    {
        if (SelectedSatellite is null)
            return;

        var index = Satellites.IndexOf(SelectedSatellite);
        Satellites.Remove(SelectedSatellite);
        SelectedSatellite = Satellites.Count == 0
            ? null
            : Satellites[Math.Min(index, Satellites.Count - 1)];
        NotifyFilteredSatellites();
        StatusMessage = "Removed satellite.";
    }

    [RelayCommand]
    private void AddMode()
    {
        if (SelectedSatellite is null)
            return;

        var mode = new SatelliteTransponderMode
        {
            Type = "New mode",
            DownlinkKHz = 435_000,
            UplinkKHz = 145_000,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "NOR"
        };
        SelectedSatellite.Modes.Add(mode);
        SelectedMode = mode;
        StatusMessage = "Added transponder mode.";
    }

    [RelayCommand]
    private void RemoveMode()
    {
        if (SelectedSatellite is null || SelectedMode is null)
            return;

        var index = SelectedSatellite.Modes.IndexOf(SelectedMode);
        SelectedSatellite.Modes.Remove(SelectedMode);
        SelectedMode = SelectedSatellite.Modes.Count == 0
            ? null
            : SelectedSatellite.Modes[Math.Min(index, SelectedSatellite.Modes.Count - 1)];
        StatusMessage = "Removed transponder mode.";
    }

    [RelayCommand]
    private void DuplicateMode()
    {
        if (SelectedSatellite is null || SelectedMode is null)
            return;

        var clone = new SatelliteTransponderMode
        {
            Type = SelectedMode.Type + " copy",
            DownlinkKHz = SelectedMode.DownlinkKHz,
            UplinkKHz = SelectedMode.UplinkKHz,
            DownlinkMode = SelectedMode.DownlinkMode,
            UplinkMode = SelectedMode.UplinkMode,
            Doppler = SelectedMode.Doppler,
            CtcssHz = SelectedMode.CtcssHz,
            CtcssArmHz = SelectedMode.CtcssArmHz
        };
        SelectedSatellite.Modes.Add(clone);
        SelectedMode = clone;
        StatusMessage = "Duplicated mode.";
    }

    public bool TrySave(out string? errorMessage)
    {
        try
        {
            _editor.Save(Satellites.ToList());
            DatabasePathText = $"User database: {_editor.UserPath}";
            StatusMessage = "Saved.";
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            StatusMessage = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private void ResetToBundled()
    {
        _editor.ResetToBundled();
        Load();
        StatusMessage = "Restored shipped database.";
    }
}
