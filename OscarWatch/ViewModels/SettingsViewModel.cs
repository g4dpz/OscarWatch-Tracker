using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Display;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Hardware;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Rotator;
using OscarWatch.Theme;

namespace OscarWatch.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ISpeechService _speech;
    private readonly GroundStation _draft = new();
    private bool _isSynchronizing;

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private double _latitudeDeg;

    [ObservableProperty]
    private double _longitudeDeg;

    [ObservableProperty]
    private double _altitudeMeters;

    [ObservableProperty]
    private string _gridSquare = "";

    [ObservableProperty]
    private double _minimumElevationDeg = 5;

    [ObservableProperty]
    private int _passPredictionHours = 48;

    [ObservableProperty]
    private AppThemePreference _themePreference = AppThemePreference.System;

    [ObservableProperty]
    private TleAutoUpdateOption? _tleAutoUpdateOption;

    [ObservableProperty]
    private bool _voiceAnnouncementsEnabled;

    [ObservableProperty]
    private double _announceElevationDeg = -3;

    [ObservableProperty]
    private SpeechVoiceOption? _selectedSpeechVoice;

    [ObservableProperty]
    private bool _rotatorEnabled;

    [ObservableProperty]
    private string? _selectedComPort;

    [ObservableProperty]
    private int _rotatorBaudRate = 4800;

    [ObservableProperty]
    private double _rotatorTrackStartElevationDeg = -3;

    [ObservableProperty]
    private double _rotatorParkAzimuthDeg;

    [ObservableProperty]
    private double _rotatorParkElevationDeg;

    public ObservableCollection<string> AvailableComPorts { get; } = [];

    public bool SpeechAvailable { get; }

    public bool SpeechUnavailable => !SpeechAvailable;

    public string VoicePreviewText { get; } = SatelliteNamePhonetics.SampleAnnouncementText;

    public IReadOnlyList<SpeechVoiceOption> SpeechVoiceOptions { get; }

    public AppThemePreference[] ThemeOptions { get; } =
        Enum.GetValues<AppThemePreference>();

    public IReadOnlyList<TleAutoUpdateOption> TleAutoUpdateOptions { get; } =
    [
        new(TleAutoUpdateMode.Manual, "Manual only"),
        new(TleAutoUpdateMode.OnStartup, "On startup (if older than 6 hours)"),
        new(TleAutoUpdateMode.EverySixHours, "Every 6 hours while running")
    ];

    public int[] BaudRateOptions { get; } = [1200, 2400, 4800, 9600, 19200];

    public IReadOnlyList<RotatorTypeOption> RotatorTypeChoices { get; } =
    [
        new(RotatorType.YaesuGs232, "Yaesu GS-232"),
        new(RotatorType.EasyComm, "EasyComm")
    ];

    [ObservableProperty]
    private RotatorTypeOption? _selectedRotatorTypeChoice;

    public IReadOnlyList<RotatorAzimuthOption> AzimuthRangeChoices { get; } =
    [
        new(RotatorAzimuthRange.Deg360, "0–360°"),
        new(RotatorAzimuthRange.Deg450, "0–450°")
    ];

    public IReadOnlyList<RotatorElevationOption> ElevationRangeChoices { get; } =
    [
        new(RotatorElevationRange.Deg90, "0–90°"),
        new(RotatorElevationRange.Deg180, "0–180°")
    ];

    [ObservableProperty]
    private RotatorAzimuthOption? _selectedAzimuthRangeChoice;

    [ObservableProperty]
    private RotatorElevationOption? _selectedElevationRangeChoice;

    [ObservableProperty]
    private bool _rigEnabled;

    [ObservableProperty]
    private string? _selectedRigComPort;

    [ObservableProperty]
    private int _rigBaudRate = 19200;

    [ObservableProperty]
    private string _rigCivAddress = "60";

    [ObservableProperty]
    private double _rigTrackStartElevationDeg = -3;

    [ObservableProperty]
    private int _rigDopplerThresholdFmHz = 200;

    [ObservableProperty]
    private int _rigDopplerThresholdLinearHz = 50;

    [ObservableProperty]
    private int _rigCatDelayMs = 50;

    [ObservableProperty]
    private RigTypeOption? _selectedRigTypeChoice;

    [ObservableProperty]
    private RigRegionOption? _selectedRigRegionChoice;

    [ObservableProperty]
    private bool _showComPortConflict;

    [ObservableProperty]
    private string _comPortConflictText = "";

    public IReadOnlyList<RigTypeOption> RigTypeChoices { get; } =
    [
        new(RigType.IcomIc910, "ICOM IC-910"),
        new(RigType.IcomIc9700, "ICOM IC-9700"),
        new(RigType.Dummy, "Dummy Rig")
    ];

    public IReadOnlyList<RigRegionOption> RigRegionChoices { get; } =
    [
        new(RigRegion.EU, "EU"),
        new(RigRegion.USA, "USA")
    ];

    public SettingsViewModel(ISettingsService settings, ISpeechService speech)
    {
        _settings = settings;
        _speech = speech;
        SpeechAvailable = speech.IsAvailable;
        SpeechVoiceOptions = speech.GetAvailableVoices();
        CopyGroundStation(settings.Current.GroundStation, _draft);
        RefreshComPorts();
        LoadFromDraft();
    }

    [RelayCommand]
    private void RefreshComPorts()
    {
        AvailableComPorts.Clear();
        foreach (var port in SerialPortDiscovery.GetAvailablePorts())
            AvailableComPorts.Add(port);

        if (SelectedComPort is not null && !AvailableComPorts.Contains(SelectedComPort))
            AvailableComPorts.Add(SelectedComPort);
        if (SelectedRigComPort is not null && !AvailableComPorts.Contains(SelectedRigComPort))
            AvailableComPorts.Add(SelectedRigComPort);
    }

    public async Task SaveAsync()
    {
        _settings.Current.GroundStation = new GroundStation
        {
            DisplayName = DisplayName,
            LatitudeDeg = LatitudeDeg,
            LongitudeDeg = LongitudeDeg,
            AltitudeMetersAsl = AltitudeMeters,
            GridSquare = GridSquare.Trim()
        };
        _settings.Current.MinimumElevationDeg = MinimumElevationDeg;
        _settings.Current.PassPredictionHours = PassPredictionHours;
        _settings.Current.Theme = ThemePreference;
        if (TleAutoUpdateOption is not null)
            _settings.Current.TleAutoUpdate = TleAutoUpdateOption.Mode;
        _settings.Current.VoiceAnnouncements = new VoiceAnnouncementSettings
        {
            Enabled = VoiceAnnouncementsEnabled,
            AnnounceElevationDeg = AnnounceElevationDeg,
            VoiceName = SelectedSpeechVoice?.Id ?? ""
        };
        _settings.Current.Rotator = new RotatorSettings
        {
            Enabled = RotatorEnabled,
            Type = SelectedRotatorTypeChoice?.Value ?? RotatorType.YaesuGs232,
            Port = SelectedComPort ?? "",
            BaudRate = RotatorBaudRate,
            AzimuthRange = SelectedAzimuthRangeChoice?.Value ?? RotatorAzimuthRange.Deg450,
            ElevationRange = SelectedElevationRangeChoice?.Value ?? RotatorElevationRange.Deg180,
            TrackStartElevationDeg = RotatorTrackStartElevationDeg,
            ParkAzimuthDeg = RotatorParkAzimuthDeg,
            ParkElevationDeg = RotatorParkElevationDeg
        };
        _settings.Current.Rig = new RigSettings
        {
            Enabled = RigEnabled,
            Type = SelectedRigTypeChoice?.Value ?? RigType.None,
            Port = SelectedRigComPort ?? "",
            BaudRate = RigBaudRate,
            CivAddress = RigCivAddress.Trim(),
            Region = SelectedRigRegionChoice?.Value ?? RigRegion.EU,
            TrackStartElevationDeg = RigTrackStartElevationDeg,
            DopplerThresholdFmHz = RigDopplerThresholdFmHz,
            DopplerThresholdLinearHz = RigDopplerThresholdLinearHz,
            CatDelayMs = RigCatDelayMs,
            CatUpdatesPaused = _settings.Current.Rig.CatUpdatesPaused
        };
        _settings.SyncActiveStationFromGroundStation();
        AppThemeManager.Apply(ThemePreference);
        await _settings.SaveAsync().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanTestVoiceAnnouncement))]
    private async Task TestVoiceAnnouncementAsync()
    {
        var voiceName = SelectedSpeechVoice?.Id;
        await _speech.SpeakAsync(
            VoicePreviewText,
            string.IsNullOrWhiteSpace(voiceName) ? null : voiceName).ConfigureAwait(true);
    }

    private bool CanTestVoiceAnnouncement() => SpeechAvailable;

    private void LoadFromDraft()
    {
        _isSynchronizing = true;
        try
        {
            DisplayName = _draft.DisplayName;
            LatitudeDeg = _draft.LatitudeDeg;
            LongitudeDeg = _draft.LongitudeDeg;
            AltitudeMeters = _draft.AltitudeMetersAsl;
            GridSquare = _draft.GridSquare;
            MinimumElevationDeg = _settings.Current.MinimumElevationDeg;
            PassPredictionHours = _settings.Current.PassPredictionHours;
            ThemePreference = _settings.Current.Theme;
            TleAutoUpdateOption = TleAutoUpdateOptions.FirstOrDefault(o => o.Mode == _settings.Current.TleAutoUpdate)
                ?? TleAutoUpdateOptions[1];

            var voice = _settings.Current.VoiceAnnouncements ?? new VoiceAnnouncementSettings();
            VoiceAnnouncementsEnabled = voice.Enabled;
            AnnounceElevationDeg = voice.AnnounceElevationDeg;
            SelectedSpeechVoice = SpeechVoiceOptions.FirstOrDefault(v => v.Id == voice.VoiceName)
                ?? SpeechVoiceOptions.FirstOrDefault();

            var rotator = _settings.Current.Rotator ?? new RotatorSettings();
            RotatorEnabled = rotator.Enabled;
            SelectedRotatorTypeChoice = RotatorTypeChoices.FirstOrDefault(o => o.Value == rotator.Type)
                ?? RotatorTypeChoices[0];
            SelectedComPort = string.IsNullOrWhiteSpace(rotator.Port) ? null : rotator.Port;
            RotatorBaudRate = rotator.BaudRate;
            SelectedAzimuthRangeChoice = AzimuthRangeChoices.FirstOrDefault(o => o.Value == rotator.AzimuthRange)
                ?? AzimuthRangeChoices[1];
            SelectedElevationRangeChoice = ElevationRangeChoices.FirstOrDefault(o => o.Value == rotator.ElevationRange)
                ?? ElevationRangeChoices[1];
            RotatorTrackStartElevationDeg = rotator.TrackStartElevationDeg;
            RotatorParkAzimuthDeg = rotator.ParkAzimuthDeg;
            RotatorParkElevationDeg = rotator.ParkElevationDeg;

            var rig = _settings.Current.Rig ?? new RigSettings();
            RigEnabled = rig.Enabled;
            SelectedRigTypeChoice = RigTypeChoices.FirstOrDefault(o => o.Value == rig.Type)
                ?? RigTypeChoices[0];
            SelectedRigComPort = string.IsNullOrWhiteSpace(rig.Port) ? null : rig.Port;
            RigBaudRate = rig.BaudRate;
            RigCivAddress = rig.CivAddress;
            SelectedRigRegionChoice = RigRegionChoices.FirstOrDefault(o => o.Value == rig.Region)
                ?? RigRegionChoices[0];
            RigTrackStartElevationDeg = rig.TrackStartElevationDeg;
            RigDopplerThresholdFmHz = rig.DopplerThresholdFmHz;
            RigDopplerThresholdLinearHz = rig.DopplerThresholdLinearHz;
            RigCatDelayMs = rig.CatDelayMs;
            RefreshComPortConflict();
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void RefreshComPortConflict()
    {
        var rotator = new RotatorSettings
        {
            Enabled = RotatorEnabled,
            Port = SelectedComPort ?? ""
        };
        var rig = new RigSettings
        {
            Enabled = RigEnabled,
            Type = SelectedRigTypeChoice?.Value ?? RigType.None,
            Port = SelectedRigComPort ?? ""
        };
        ShowComPortConflict = SerialPortConflictHelper.TryDescribeConflict(rotator, rig, out var message);
        ComPortConflictText = message;
    }

    partial void OnRotatorEnabledChanged(bool value) => RefreshComPortConflictIfReady();
    partial void OnRigEnabledChanged(bool value) => RefreshComPortConflictIfReady();
    partial void OnSelectedComPortChanged(string? value) => RefreshComPortConflictIfReady();
    partial void OnSelectedRigComPortChanged(string? value) => RefreshComPortConflictIfReady();
    partial void OnSelectedRigTypeChoiceChanged(RigTypeOption? value)
    {
        RefreshComPortConflictIfReady();
        if (_isSynchronizing || value is null)
            return;
        var suggested = RigSettings.DefaultCivAddressFor(value.Value);
        if (string.IsNullOrWhiteSpace(RigCivAddress)
            || RigCivAddress is "60" or "7C" or "A2")
            RigCivAddress = suggested;
    }

    private void RefreshComPortConflictIfReady()
    {
        if (_isSynchronizing)
            return;
        RefreshComPortConflict();
    }

    private static void CopyGroundStation(GroundStation source, GroundStation target)
    {
        target.DisplayName = source.DisplayName;
        target.LatitudeDeg = source.LatitudeDeg;
        target.LongitudeDeg = source.LongitudeDeg;
        target.AltitudeMetersAsl = source.AltitudeMetersAsl;
        target.GridSquare = source.GridSquare;
    }

    private void SyncGridFromDraftLatLon()
    {
        _draft.LatitudeDeg = LatitudeDeg;
        _draft.LongitudeDeg = LongitudeDeg;
        _draft.GridSquare = MaidenheadGrid.FromLatLon(LatitudeDeg, LongitudeDeg);
        var updated = _draft.GridSquare;
        if (!string.Equals(GridSquare, updated, StringComparison.Ordinal))
            GridSquare = updated;
    }

    partial void OnLatitudeDegChanged(double value)
    {
        if (_isSynchronizing)
            return;

        _isSynchronizing = true;
        try
        {
            SyncGridFromDraftLatLon();
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    partial void OnLongitudeDegChanged(double value)
    {
        if (_isSynchronizing)
            return;

        _isSynchronizing = true;
        try
        {
            SyncGridFromDraftLatLon();
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    partial void OnThemePreferenceChanged(AppThemePreference value)
    {
        if (_isSynchronizing)
            return;

        AppThemeManager.Apply(value);
    }

    partial void OnGridSquareChanged(string value)
    {
        if (_isSynchronizing || string.IsNullOrWhiteSpace(value) || value.Length < 4)
            return;

        _isSynchronizing = true;
        try
        {
            _draft.GridSquare = value.Trim();
            var (lat, lon) = MaidenheadGrid.ToLatLonCenter(_draft.GridSquare);
            _draft.LatitudeDeg = lat;
            _draft.LongitudeDeg = lon;
            if (!LatitudeDeg.Equals(lat))
                LatitudeDeg = lat;
            if (!LongitudeDeg.Equals(lon))
                LongitudeDeg = lon;
        }
        catch
        {
            // invalid grid square
        }
        finally
        {
            _isSynchronizing = false;
        }
    }
}

public sealed record TleAutoUpdateOption(TleAutoUpdateMode Mode, string Label);

public sealed record RotatorTypeOption(RotatorType Value, string Label);

public sealed record RotatorAzimuthOption(RotatorAzimuthRange Value, string Label);

public sealed record RotatorElevationOption(RotatorElevationRange Value, string Label);

public sealed record RigTypeOption(RigType Value, string Label);

public sealed record RigRegionOption(RigRegion Value, string Label);
