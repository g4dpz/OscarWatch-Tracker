using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OscarWatch.Core.Display;
using OscarWatch.Core.Hardware;
using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
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
    private readonly IRotatorController _rotator;
    private readonly IRigController _rig;
    private readonly ICloudlogRadioSyncService _cloudlog;
    private readonly DispatcherTimer _timer;
    private string? _lastCloudlogErrorShown;

    public FrequencyOverlayViewModel Frequencies { get; }
    private DispatcherTimer? _tleRefreshTimer;
    private DispatcherTimer? _passListRefreshTimer;
    private DispatcherTimer? _rigContextTimer;
    private static readonly TimeSpan PassListRefreshInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RigContextInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ImminentPassWindow = TimeSpan.FromMinutes(15);
    /// <summary>Coalesce spinner clicks into one CAT write after the user pauses.</summary>

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
    private bool _showRotatorStatus;

    [ObservableProperty]
    private string _rotatorAzimuthText = "—";

    [ObservableProperty]
    private string _rotatorElevationText = "—";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ParkRotatorCommand))]
    private bool _canParkRotator;

    [ObservableProperty]
    private bool _showRigStatus;

    [ObservableProperty]
    private string _rigStatusText = "—";

    [ObservableProperty]
    private string _rigReceiveText = "—";

    [ObservableProperty]
    private string _rigTransmitText = "—";

    [ObservableProperty]
    private bool _rigCatPaused;

    [ObservableProperty]
    private bool _showComPortConflict;

    [ObservableProperty]
    private string _comPortConflictText = "";

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
        RisingPassAnnouncer passAnnouncer,
        IRotatorController rotator,
        IRigController rig,
        ICloudlogRadioSyncService cloudlog,
        FrequencyOverlayViewModel frequencies)
    {
        _settings = settings;
        _tleService = tleService;
        _tracking = tracking;
        _speech = speech;
        _passAnnouncer = passAnnouncer;
        _rotator = rotator;
        _rig = rig;
        _cloudlog = cloudlog;
        Frequencies = frequencies;
        Frequencies.OffsetsChanged += (_, _) => RefreshRigFromOverlay();
        Frequencies.CtcssChanged += (_, _) => OnCtcssSelectorChanged();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();

        _rigContextTimer = new DispatcherTimer { Interval = RigContextInterval };
        _rigContextTimer.Tick += (_, _) => PublishRigTrackingContext();
    }

    private void OnCtcssSelectorChanged()
    {
        if (ShowComPortConflict)
            return;

        var focused = GetFocusedTrackState(_tracking.GetLiveStates(DateTime.UtcNow), FocusedNoradId);
        var context = Frequencies.TryBuildRigTrackingContext(focused);
        _rig.ApplySelectedCtcss(_settings.Current.Rig, context);
        _rig.PublishContext(_settings.Current.Rig, context);
        RefreshRigUi(focused);
    }

    private void RefreshRigFromOverlay()
    {
        if (ShowComPortConflict)
            return;

        var focused = GetFocusedTrackState(_tracking.GetLiveStates(DateTime.UtcNow), FocusedNoradId);
        var context = Frequencies.TryBuildRigTrackingContext(focused);
        _rig.Update(_settings.Current.Rig, context);
        RefreshRigUi(focused);
    }

    private void RefreshRigUi(SatelliteTrackState? focused)
    {
        var rigStatus = _rig.GetStatus();
        Frequencies.SyncRigPassbandAdjustments(rigStatus.ManualReceiveAdjustKHz, rigStatus.ManualTransmitAdjustKHz);
        UpdateRigDisplay(rigStatus);
        PushCloudlogRadio(focused);
    }

    public async Task InitializeAsync()
    {
        StatusText = "Loading settings…";
        await _settings.LoadAsync().ConfigureAwait(true);
        AppThemeManager.Apply(_settings.Current.Theme);
        RefreshGroundStationFromSettings();
        RigCatPaused = _settings.Current.Rig.CatUpdatesPaused;

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
        ConfigurePassListRefreshTimer();
        ConfigureRigContextTimer();

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
        PruneExpiredPasses();
        UpdatePassHighlightState();
        ProcessVoiceAnnouncements(states);
        var focused = GetFocusedTrackState(states, FocusedNoradId);
        UpdateComPortConflictState();
        _rotator.Update(_settings.Current.Rotator, focused);
        UpdateRotatorDisplay();
        Frequencies.Update(focused);

        if (ShowComPortConflict)
            _rig.Disconnect();
        else
            PublishRigTrackingContext(focused);

        RefreshRigUi(focused);
    }

    /// <summary>Refresh look angles / range rate for the doppler loop (4 Hz). UI still ticks at 1 Hz.</summary>
    private void PublishRigTrackingContext(SatelliteTrackState? focused = null)
    {
        if (!_settings.Current.Rig.Enabled || ShowComPortConflict)
            return;

        focused ??= GetFocusedTrackState(_tracking.GetLiveStates(DateTime.UtcNow), FocusedNoradId);
        _rig.PublishContext(_settings.Current.Rig, Frequencies.TryBuildRigTrackingContext(focused));
    }

    private void ConfigureRigContextTimer()
    {
        if (_settings.Current.Rig.Enabled)
            _rigContextTimer?.Start();
        else
            _rigContextTimer?.Stop();
    }

    private void PushCloudlogRadio(SatelliteTrackState? focused)
    {
        var update = Frequencies.TryBuildCloudlogUpdate(focused);
        _cloudlog.Publish(_settings.Current.Cloudlog, update);

        var error = _cloudlog.LastError;
        if (!string.IsNullOrEmpty(error) && !string.Equals(_lastCloudlogErrorShown, error, StringComparison.Ordinal))
        {
            _lastCloudlogErrorShown = error;
            StatusText = $"Cloudlog: {error}";
        }
        else if (string.IsNullOrEmpty(error))
            _lastCloudlogErrorShown = null;
    }

    private void UpdateComPortConflictState()
    {
        ShowComPortConflict = SerialPortConflictHelper.TryDescribeConflict(
            _settings.Current.Rotator,
            _settings.Current.Rig,
            out var message);
        ComPortConflictText = message;
    }

    partial void OnRigCatPausedChanged(bool value)
    {
        if (_settings.Current.Rig.CatUpdatesPaused == value)
            return;

        _settings.Current.Rig.CatUpdatesPaused = value;
        _ = _settings.SaveAsync();
    }

    private void UpdateRotatorDisplay()
    {
        if (!_settings.Current.Rotator.Enabled)
        {
            ShowRotatorStatus = false;
            return;
        }

        ShowRotatorStatus = true;
        var status = _rotator.GetPositionStatus();
        RotatorAzimuthText = status is { IsConnected: true, AzimuthDeg: not null }
            ? $"{status.AzimuthDeg.Value}°"
            : "—";
        RotatorElevationText = status is { IsConnected: true, ElevationDeg: not null }
            ? $"{status.ElevationDeg.Value}°"
            : "—";
        CanParkRotator = status.IsConnected;
    }

    [RelayCommand(CanExecute = nameof(CanParkRotator))]
    private void ParkRotator()
    {
        _rotator.Park(_settings.Current.Rotator);
        UpdateRotatorDisplay();
    }

    private void UpdateRigDisplay(RigConnectionStatus? status = null)
    {
        if (!_settings.Current.Rig.Enabled)
        {
            ShowRigStatus = false;
            return;
        }

        ShowRigStatus = true;
        if (ShowComPortConflict)
        {
            RigStatusText = ComPortConflictText;
            RigReceiveText = "—";
            RigTransmitText = "—";
            return;
        }

        status ??= _rig.GetStatus();
        RigCatPaused = status.CatUpdatesPaused;
        RigStatusText = status.StatusMessage ?? (status.IsConnected ? "Connected" : "Disconnected");
        RigReceiveText = FormatSidebarFrequency(status.LastReceiveHz, Frequencies.RadioReceiveText, status.IsConnected);
        RigTransmitText = FormatSidebarFrequency(status.LastTransmitHz, Frequencies.RadioTransmitText, status.IsConnected);
    }

    private static string FormatSidebarFrequency(long? rigHz, string overlayText, bool rigConnected)
    {
        if (rigHz is { } hz && IcomCivCodec.IsValidSatelliteFrequencyHz(hz))
            return FrequencyDisplayFormat.FormatMHz(hz / 1000.0);

        if (rigConnected && !string.IsNullOrWhiteSpace(overlayText) && overlayText != "—")
            return overlayText;

        return "—";
    }

    private void ConfigurePassListRefreshTimer()
    {
        _passListRefreshTimer?.Stop();
        _passListRefreshTimer = new DispatcherTimer { Interval = PassListRefreshInterval };
        _passListRefreshTimer.Tick += async (_, _) => await RefreshPassesAsync();
        _passListRefreshTimer.Start();
    }

    private void PruneExpiredPasses()
    {
        var now = DateTime.UtcNow;
        var expired = Passes.OfType<PassRowViewModel>().Where(p => p.LosUtc < now).ToList();
        if (expired.Count == 0)
            return;

        foreach (var pass in expired)
            Passes.Remove(pass);

        for (var i = Passes.Count - 1; i >= 0; i--)
        {
            if (Passes[i] is PassDayHeaderViewModel
                && (i + 1 >= Passes.Count || Passes[i + 1] is PassDayHeaderViewModel))
                Passes.RemoveAt(i);
        }
    }

    private static SatelliteTrackState? GetFocusedTrackState(IReadOnlyList<SatelliteTrackState> states, string? focusedNoradId) =>
        states.FirstOrDefault(s => s.NoradId == focusedNoradId)
        ?? states
            .Where(s => s.LookAngles is { ElevationDeg: > 0 })
            .OrderByDescending(s => s.LookAngles!.ElevationDeg)
            .FirstOrDefault()
        ?? states.FirstOrDefault();

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
        var target = GetFocusedTrackState(states, FocusedNoradId);

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
        {
            UpdateLiveTelemetry(LiveStates);
            var focused = string.IsNullOrEmpty(value)
                ? null
                : LiveStates.FirstOrDefault(s => s.NoradId == value);
            Frequencies.Update(focused);
            PushCloudlogRadio(focused);
            RefreshRigFromOverlay();
        }

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
        var catalogCount = _tleService.Catalog.Count;
        var count = _tleService.GetEnabledSatellites(_settings.Current).Count;
        var tleAge = _tleService.LastFetchedUtc.HasValue
            ? $"TLE {DateTime.UtcNow - _tleService.LastFetchedUtc.Value:hh\\:mm} ago"
            : catalogCount > 0 ? "TLE bundled seed" : "TLE not loaded";

        StatusText = catalogCount == 0
            ? "No TLE data — check network and use Refresh TLEs"
            : count == 0
                ? $"{tleAge} | 0 enabled — Satellites menu → enable some"
                : $"{tleAge} | {count} satellite(s) enabled";
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
            _rotator.Disconnect();
            _rig.Disconnect();
            _cloudlog.ResetThrottle();
            await RefreshPassesAsync();
            UpdateStatus();
            RefreshGroundStationFromSettings();
            RigCatPaused = _settings.Current.Rig.CatUpdatesPaused;
            ConfigureRigContextTimer();
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
    private void CloseApplication()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    [RelayCommand]
    private async Task OpenAboutAsync()
    {
        var window = new AboutWindow();
        if (App.MainWindow is null)
            window.Show();
        else
            await window.ShowDialog(App.MainWindow);
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
    private async Task OpenSatelliteDatabaseAsync()
    {
        var vm = App.Services.GetRequiredService<SatelliteDatabaseEditorViewModel>();
        var window = new SatelliteDatabaseWindow { DataContext = vm };
        if (App.MainWindow is null)
            return;

        var saved = await window.ShowDialog<bool?>(App.MainWindow) == true;
        if (saved)
        {
            Frequencies.ReloadFromDatabase();
            Tick();
            StatusText = "Transponder database saved.";
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
