using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Display;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
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

    public SettingsViewModel(ISettingsService settings, ISpeechService speech)
    {
        _settings = settings;
        _speech = speech;
        SpeechAvailable = speech.IsAvailable;
        SpeechVoiceOptions = speech.GetAvailableVoices();
        CopyGroundStation(settings.Current.GroundStation, _draft);
        LoadFromDraft();
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
        }
        finally
        {
            _isSynchronizing = false;
        }
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
