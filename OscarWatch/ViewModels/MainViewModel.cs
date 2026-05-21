using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Theme;
using OscarWatch.Views;

namespace OscarWatch.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ITleService _tleService;
    private readonly TrackingOrchestrator _tracking;
    private readonly ISpeechService _speech;
    private readonly RisingPassAnnouncer _passAnnouncer;
    private readonly DispatcherTimer _timer;
    private DispatcherTimer? _tleRefreshTimer;
    private static readonly TimeSpan ImminentPassWindow = TimeSpan.FromMinutes(15);

    [ObservableProperty]
    private string _statusText = "Starting…";

    [ObservableProperty]
    private string _utcClock = "";

    [ObservableProperty]
    private string _selectedSatelliteName = "—";

    [ObservableProperty]
    private string _azimuthText = "—";

    [ObservableProperty]
    private string _elevationText = "—";

    [ObservableProperty]
    private string _rangeText = "—";

    [ObservableProperty]
    private string _altitudeText = "—";

    [ObservableProperty]
    private string _nextPassText = "—";

    [ObservableProperty]
    private string? _focusedNoradId;

    public ObservableCollection<IPassListItem> Passes { get; } = [];

    [ObservableProperty]
    private IPassListItem? _selectedListItem;
    private readonly ObservableCollection<SatelliteTrackState> _liveStates = [];

    public ObservableCollection<SatelliteTrackState> LiveStates => _liveStates;

    [ObservableProperty]
    private GroundStation _groundStation = new();

    [ObservableProperty]
    private double _minimumElevationDeg = 5;

    public MainViewModel(
        ISettingsService settings,
        ITleService tleService,
        TrackingOrchestrator tracking,
        ISpeechService speech,
        RisingPassAnnouncer passAnnouncer)
    {
        _settings = settings;
        _tleService = tleService;
        _tracking = tracking;
        _speech = speech;
        _passAnnouncer = passAnnouncer;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
    }

    public async Task InitializeAsync()
    {
        StatusText = "Loading settings…";
        await _settings.LoadAsync().ConfigureAwait(true);
        AppThemeManager.Apply(_settings.Current.Theme);
        RefreshGroundStationFromSettings();

        StatusText = "Loading TLE catalog…";
        await _tleService.EnsureLoadedAsync().ConfigureAwait(true);

        if (_tleService.IsStale(_settings.Current.TleStaleHours))
        {
            try
            {
                StatusText = "Refreshing TLEs…";
                await _tleService.RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StatusText = $"TLE refresh failed: {ex.Message}";
            }
        }

        _tracking.ReloadEnabledSatellites();
        Tick();
        _timer.Start();

        StatusText = "Computing passes…";
        await RefreshPassesAsync().ConfigureAwait(true);
        UpdateStatus();
        Tick();
    }

    private void Tick()
    {
        UtcClock = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        MinimumElevationDeg = _settings.Current.MinimumElevationDeg;
        var states = _tracking.GetLiveStates(DateTime.UtcNow);
        SyncLiveStates(states);

        UpdateLiveTelemetry(states);
        UpdateNextPassCountdown();
        UpdatePassHighlightState();
        ProcessVoiceAnnouncements(states);
    }

    private void ProcessVoiceAnnouncements(IReadOnlyList<SatelliteTrackState> states)
    {
        var voiceSettings = _settings.Current.VoiceAnnouncements;
        if (voiceSettings is null || !voiceSettings.Enabled)
            return;

        _passAnnouncer.Process(states, voiceSettings, text =>
        {
            var voiceName = voiceSettings.VoiceName;
            _ = SpeakAnnouncementAsync(text, voiceName);
        });
    }

    private async Task SpeakAnnouncementAsync(string text, string voiceName)
    {
        try
        {
            await _speech.SpeakAsync(
                text,
                string.IsNullOrWhiteSpace(voiceName) ? null : voiceName).ConfigureAwait(false);
        }
        catch
        {
            // speech failures should not interrupt tracking
        }
    }

    private void UpdatePassHighlightState()
    {
        var now = DateTime.UtcNow;
        foreach (var pass in Passes.OfType<PassRowViewModel>())
            pass.UpdateHighlight(now, ImminentPassWindow);
    }

    private void SyncLiveStates(IReadOnlyList<SatelliteTrackState> states)
    {
        _liveStates.Clear();
        foreach (var s in states)
            _liveStates.Add(s);
    }

    private void UpdateLiveTelemetry(IReadOnlyList<SatelliteTrackState> states)
    {
        var target = states.FirstOrDefault(s => s.NoradId == FocusedNoradId)
            ?? states
                .Where(s => s.LookAngles is { ElevationDeg: > 0 })
                .OrderByDescending(s => s.LookAngles!.ElevationDeg)
                .FirstOrDefault()
            ?? states.FirstOrDefault();

        if (target is null)
            return;

        SelectedSatelliteName = target.Name;
        AltitudeText = $"{target.Subpoint.AltitudeKm:F0} km";

        if (target.LookAngles is not { } la)
        {
            AzimuthText = "—";
            ElevationText = "below horizon";
            RangeText = "—";
            return;
        }

        AzimuthText = $"{la.AzimuthDeg:F1}°";
        ElevationText = $"{la.ElevationDeg:F1}°";
        RangeText = $"{la.RangeKm:F0} km";
    }

    partial void OnSelectedListItemChanged(IPassListItem? value)
    {
        if (value is PassRowViewModel row)
            FocusedNoradId = row.NoradId;
    }

    partial void OnFocusedNoradIdChanged(string? value)
    {
        if (LiveStates.Count > 0)
            UpdateLiveTelemetry(LiveStates);

        if (value is null)
            return;

        var pass = Passes.OfType<PassRowViewModel>().FirstOrDefault(p => p.NoradId == value);
        if (pass is not null && !ReferenceEquals(SelectedListItem, pass))
            SelectedListItem = pass;
    }

    private void UpdateNextPassCountdown()
    {
        var next = Passes.OfType<PassRowViewModel>().FirstOrDefault();
        if (next is null)
        {
            NextPassText = "No upcoming passes";
            return;
        }

        var aos = next.AosUtc;
        var delta = aos - DateTime.UtcNow;
        if (delta.TotalSeconds < 0)
            NextPassText = $"{next.SatelliteName} — in progress";
        else
            NextPassText = $"{next.SatelliteName} AOS in {delta:hh\\:mm\\:ss}";
    }

    private void UpdateStatus()
    {
        var count = _tleService.GetEnabledSatellites(_settings.Current).Count;
        var tleAge = _tleService.LastFetchedUtc.HasValue
            ? $"TLE {DateTime.UtcNow - _tleService.LastFetchedUtc.Value:hh\\:mm} ago"
            : "TLE not fetched";
        StatusText = $"{tleAge} | {count} satellite(s) enabled";
    }

    [RelayCommand]
    private async Task OpenPassPlanningAsync()
    {
        var vm = App.Services.GetRequiredService<PassPlanningViewModel>();
        vm.Initialize();
        var window = new PassPlanningWindow { DataContext = vm };
        if (App.MainWindow is null)
            return;

        var appliedActive = await window.ShowDialog<bool?>(App.MainWindow) == true;
        if (appliedActive)
            RefreshGroundStationFromSettings();

        await RefreshPassesAsync();
        if (appliedActive)
            Tick();
    }

    [RelayCommand]
    private async Task OpenMutualPassFinderAsync()
    {
        var vm = App.Services.GetRequiredService<MutualPassViewModel>();
        vm.Initialize();
        var window = new MutualPassWindow { DataContext = vm };
        if (App.MainWindow is null)
            return;

        await window.ShowDialog(App.MainWindow);
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var vm = App.Services.GetRequiredService<SettingsViewModel>();
        var window = new SettingsWindow { DataContext = vm };
        if (App.MainWindow is null)
            return;

        var saved = await window.ShowDialog<bool?>(App.MainWindow) == true;

        if (saved)
        {
            ConfigureTleAutoUpdateTimer();
            _tracking.ReloadEnabledSatellites();
            await RefreshPassesAsync();
            UpdateStatus();
            RefreshGroundStationFromSettings();
            Tick();
        }
    }

    private void ConfigureTleAutoUpdateTimer()
    {
        _tleRefreshTimer?.Stop();
        _tleRefreshTimer = null;

        if (_settings.Current.TleAutoUpdate != TleAutoUpdateMode.EverySixHours)
            return;

        _tleRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromHours(TleAutoUpdate.IntervalHours)
        };
        _tleRefreshTimer.Tick += async (_, _) => await MaybeAutoRefreshTlesAsync(force: true);
        _tleRefreshTimer.Start();
    }

    private async Task MaybeAutoRefreshTlesAsync(bool force = false)
    {
        var mode = _settings.Current.TleAutoUpdate;
        if (mode == TleAutoUpdateMode.Manual && !force)
            return;

        if (!force && !TleAutoUpdate.ShouldRefreshOnStartup(mode))
            return;

        if (!force && !_tleService.IsStale(TleAutoUpdate.IntervalHours))
            return;

        try
        {
            StatusText = "Refreshing TLEs…";
            await _tleService.RefreshAsync().ConfigureAwait(true);
            _tracking.ReloadEnabledSatellites();
        }
        catch (Exception ex)
        {
            StatusText = $"TLE refresh failed: {ex.Message}";
        }
    }

    private void RefreshGroundStationFromSettings()
    {
        var gs = _settings.Current.GroundStation;
        GroundStation = new GroundStation
        {
            DisplayName = gs.DisplayName,
            LatitudeDeg = gs.LatitudeDeg,
            LongitudeDeg = gs.LongitudeDeg,
            AltitudeMetersAsl = gs.AltitudeMetersAsl,
            GridSquare = gs.GridSquare
        };
    }

    [RelayCommand]
    private async Task OpenSatellitesAsync()
    {
        await _tleService.EnsureLoadedAsync();
        var vm = new SatellitePickerViewModel(_settings, _tleService);
        var window = new SatellitePickerWindow { DataContext = vm };
        var saved = App.MainWindow is not null
            && await window.ShowDialog<bool>(App.MainWindow);

        if (saved)
        {
            _tracking.ReloadEnabledSatellites();
            await RefreshPassesAsync();
            UpdateStatus();
        }
    }

    [RelayCommand]
    private async Task RefreshTlesAsync()
    {
        await MaybeAutoRefreshTlesAsync(force: true);
        await RefreshPassesAsync();
        UpdateStatus();
    }

    private async Task RefreshPassesAsync()
    {
        var selectedNorad = (SelectedListItem as PassRowViewModel)?.NoradId;
        Passes.Clear();
        var passes = await _tracking.GetUpcomingPassesAsync();
        DateOnly? currentDay = null;
        foreach (var p in passes.Take(50))
        {
            var day = PassDisplayFormat.GetLocalDate(p.AosUtc);
            if (currentDay != day)
            {
                currentDay = day;
                Passes.Add(new PassDayHeaderViewModel
                {
                    DateLabel = PassDisplayFormat.FormatDayHeader(p.AosUtc)
                });
            }

            Passes.Add(PassRowViewModel.From(p));
        }

        if (selectedNorad is not null)
            SelectedListItem = Passes.OfType<PassRowViewModel>().FirstOrDefault(p => p.NoradId == selectedNorad);

        UpdatePassHighlightState();
    }
}

public interface IPassListItem;

public sealed class PassDayHeaderViewModel : IPassListItem
{
    public string DateLabel { get; init; } = "";
}

public partial class PassRowViewModel : ObservableObject, IPassListItem
{
    [ObservableProperty]
    private PassRowHighlight _highlight;

    [ObservableProperty]
    private string _badgeText = "";

    [ObservableProperty]
    private bool _showBadge;

    public string SatelliteName { get; init; } = "";
    public string NoradId { get; init; } = "";
    public string AosLocal { get; init; } = "";
    public string LosLocal { get; init; } = "";
    public string TcaLocal { get; init; } = "";
    public string TimeRangeLine { get; init; } = "";
    public string DetailsLine { get; init; } = "";
    public DateTime AosUtc { get; init; }
    public DateTime LosUtc { get; init; }

    public void UpdateHighlight(DateTime utcNow, TimeSpan imminentWindow)
    {
        PassRowHighlight next;
        if (utcNow >= AosUtc && utcNow <= LosUtc)
            next = PassRowHighlight.InProgress;
        else if (utcNow < AosUtc && AosUtc - utcNow <= imminentWindow)
            next = PassRowHighlight.Imminent;
        else
            next = PassRowHighlight.None;

        if (Highlight != next)
            Highlight = next;

        if (next == PassRowHighlight.Imminent)
        {
            var countdown = PassDisplayFormat.FormatCountdownToAos(utcNow, AosUtc);
            if (BadgeText != countdown)
                BadgeText = countdown;
            if (!ShowBadge)
                ShowBadge = true;
        }
        else
        {
            if (ShowBadge)
                ShowBadge = false;
            if (BadgeText.Length > 0)
                BadgeText = "";
        }
    }

    public static PassRowViewModel From(PassInfo p)
    {
        var (aos, los) = PassDisplayFormat.FormatLocalTimes(p.AosUtc, p.LosUtc);

        return new()
        {
            SatelliteName = p.SatelliteName,
            NoradId = p.NoradId,
            AosUtc = p.AosUtc,
            LosUtc = p.LosUtc,
            AosLocal = aos,
            LosLocal = los,
            TcaLocal = PassDisplayFormat.FormatLocal(p.MaxElevationUtc),
            TimeRangeLine = PassDisplayFormat.FormatTimeRangeLine(p.AosUtc, p.LosUtc),
            DetailsLine = PassDisplayFormat.FormatDetailsLine(p.MaxElevationDeg, p.Duration)
        };
    }
}
