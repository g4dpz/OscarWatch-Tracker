using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Views;

namespace OscarWatch.ViewModels;

public partial class SatelliteDatabaseEditorViewModel : ViewModelBase
{
    private readonly ISatelliteDatabaseEditor _editor;
    private readonly ISatelliteDatabaseSyncService _syncService;
    private readonly ITleService _tleService;
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

    public SatelliteDatabaseEditorViewModel(
        ISatelliteDatabaseEditor editor,
        ISatelliteDatabaseSyncService syncService,
        ITleService tleService)
    {
        _editor = editor;
        _syncService = syncService;
        _tleService = tleService;
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
    private async Task AddSatelliteAsync()
    {
        if (App.MainWindow is null)
            return;

        var existing = Satellites.Select(s => s.Name).ToList();
        var name = await AddSatelliteFromTleDialog.TryPickAsync(App.MainWindow, _tleService, existing)
            .ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Add satellite cancelled.";
            return;
        }

        var trimmed = name.Trim();
        if (Satellites.Any(s => s.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Satellite “{trimmed}” is already in the database.";
            return;
        }

        var entry = new SatelliteRadioEntry
        {
            Name = trimmed,
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
        StatusMessage = $"Added {trimmed}.";
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

    private static readonly FilePickerFileType JsonFileType = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (App.MainWindow is null)
            return;

        var storage = TopLevel.GetTopLevel(App.MainWindow)?.StorageProvider;
        if (storage is null)
            return;

        var validationError = SatelliteDatabaseFile.ValidateEntries(Satellites);
        if (validationError is not null)
        {
            StatusMessage = validationError;
            return;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export transponder database",
            SuggestedFileName = "satellite_database.json",
            DefaultExtension = "json",
            FileTypeChoices = [JsonFileType]
        }).ConfigureAwait(true);

        if (file is null)
            return;

        try
        {
            var json = SatelliteDatabaseFile.SerializeEntries(Satellites);
            await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json).ConfigureAwait(true);
            StatusMessage = $"Exported {Satellites.Count} satellites to JSON.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportJsonAsync()
    {
        if (App.MainWindow is null)
            return;

        var storage = TopLevel.GetTopLevel(App.MainWindow)?.StorageProvider;
        if (storage is null)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import transponder database",
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        }).ConfigureAwait(true);

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        try
        {
            StatusMessage = "Reading import file…";
            await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync().ConfigureAwait(true);
            var imported = SatelliteDatabaseFile.ParseJson(json);
            var validationError = SatelliteDatabaseFile.ValidateEntries(imported);
            if (validationError is not null)
            {
                StatusMessage = $"Invalid database: {validationError}";
                return;
            }

            var local = SnapshotSatellites();
            var plan = SatelliteDatabaseMerger.BuildPlan(local, imported);
            if (!plan.HasChanges)
            {
                StatusMessage = "Import file matches the editor (no new or changed entries).";
                return;
            }

            var merged = await TransponderDatabaseMergeDialog.TryMergeApplyAsync(
                App.MainWindow,
                plan,
                local,
                SatelliteDatabaseMergePresentation.FileImport).ConfigureAwait(true);

            if (merged is null)
            {
                StatusMessage = "Import cancelled.";
                return;
            }

            ReloadFromList(merged);
            StatusMessage = "Import applied to editor — press Save to write to your user database.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (App.MainWindow is null)
            return;

        try
        {
            StatusMessage = "Checking for transponder database updates…";
            var plan = await _syncService.FetchMergePlanAsync().ConfigureAwait(true);
            if (!plan.HasChanges)
            {
                StatusMessage = "Transponder database is up to date.";
                return;
            }

            if (await TransponderDatabaseMergeDialog.TryShowAsync(App.MainWindow, plan, _syncService))
            {
                Load();
                StatusMessage = "Updates applied.";
            }
            else
            {
                StatusMessage = "No changes applied.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update check failed: {ex.Message}";
        }
    }

    private List<SatelliteRadioEntry> SnapshotSatellites() =>
        Satellites.Select(CloneEntry).ToList();

    private void ReloadFromList(IReadOnlyList<SatelliteRadioEntry> entries)
    {
        Satellites.Clear();
        foreach (var entry in entries)
            Satellites.Add(CloneEntry(entry));

        NotifyFilteredSatellites();
        SelectedSatellite = Satellites.FirstOrDefault();
        StatusMessage = $"{Satellites.Count} satellites in editor.";
    }

    private static SatelliteRadioEntry CloneEntry(SatelliteRadioEntry source) =>
        new()
        {
            Name = source.Name,
            Modes = source.Modes.Select(CloneMode).ToList()
        };

    private static SatelliteTransponderMode CloneMode(SatelliteTransponderMode source) =>
        new()
        {
            Type = source.Type,
            DownlinkKHz = source.DownlinkKHz,
            UplinkKHz = source.UplinkKHz,
            DownlinkMode = source.DownlinkMode,
            UplinkMode = source.UplinkMode,
            Doppler = source.Doppler,
            CtcssHz = source.CtcssHz,
            CtcssArmHz = source.CtcssArmHz
        };
}
