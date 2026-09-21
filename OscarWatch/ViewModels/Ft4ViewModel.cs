using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Controls;
using OscarWatch.Core.Ft4;
using OscarWatch.Core.Services;
using OscarWatch.Ft4;
using OscarWatch.Localization;
using OscarWatch.Rotator;
using Serilog;

namespace OscarWatch.ViewModels;

/// <summary>FT4 modem window: decode list, TX controls, and Doppler status.</summary>
public partial class Ft4ViewModel : ViewModelBase, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<Ft4ViewModel>();

    private readonly ISettingsService _settings;
    private readonly FrequencyOverlayViewModel _frequencyOverlay;
    private readonly ILiveTrackerSnapshotProvider _tracker;
    private readonly ILocalizationService _l;
    private readonly Ft4ModemService _modem;
    private readonly DispatcherTimer _uiTimer;
    private bool _disposed;
    private bool _loadingDevices;

    public Ft4ViewModel(
        ISettingsService settings,
        FrequencyOverlayViewModel frequencyOverlay,
        ILiveTrackerSnapshotProvider tracker,
        ILocalizationService localization,
        Ft4ModemService modem)
    {
        _settings = settings;
        _frequencyOverlay = frequencyOverlay;
        _tracker = tracker;
        _l = localization;
        _modem = modem;

        PttMethodOptions =
        [
            new Ft4PttMethodOption(Ft4PttMethod.Vox, _l.Get("Ft4.Ptt.Vox")),
            new Ft4PttMethodOption(Ft4PttMethod.Cat, _l.Get("Ft4.Ptt.Cat")),
            new Ft4PttMethodOption(Ft4PttMethod.CatPortHandshake, _l.Get("Ft4.Ptt.CatPortHandshake")),
            new Ft4PttMethodOption(Ft4PttMethod.SeparateComPort, _l.Get("Ft4.Ptt.SeparateComPort")),
            new Ft4PttMethodOption(Ft4PttMethod.Manual, _l.Get("Ft4.Ptt.Manual")),
        ];

        PttLineOptions =
        [
            new Ft4PttLineOption(Ft4PttLine.Rts, _l.Get("Ft4.Ptt.Rts")),
            new Ft4PttLineOption(Ft4PttLine.Dtr, _l.Get("Ft4.Ptt.Dtr")),
        ];

        var ft4 = _settings.Current.Ft4;
        _skipRrr = ft4.SkipRrr;
        _txAudioHz = Math.Clamp(ft4.TxAudioHz, 200, 3000);
        _txLevel = Math.Clamp(ft4.TxLevel, 0.05, 1.0);
        _holdTxFrequency = ft4.HoldTxFrequency;
        _audioDopplerTx = ft4.AudioDopplerTx;
        _audioDopplerRx = ft4.AudioDopplerRx;
        _pttLeadMs = Math.Clamp(ft4.PttLeadMs, 0, 2000);
        _pttTailMs = Math.Clamp(ft4.PttTailMs, 0, 2000);
        _decodeFontSize = Math.Clamp(ft4.DecodeFontSize, 10, 28);
        _preferEvenSlot = false;
        _pttInvert = ft4.PttInvert;
        _selectedPttMethod = PttMethodOptions.FirstOrDefault(o => o.Value == ft4.PttMethod)
            ?? PttMethodOptions[0];
        _selectedPttLine = PttLineOptions.FirstOrDefault(o => o.Value == ft4.PttLine)
            ?? PttLineOptions[0];
        _separatePttPort = ft4.SeparatePttPort ?? "";

        StatusLine = _l.Get("Ft4.Status.Idle");
        WaterfallStatusText = _l.Get("Ft4.Waterfall.Unavailable");
        SlotClockText = "-";
        DopplerUplinkText = "-";
        DopplerDownlinkText = "-";

        RefreshAudioDevices();
        RefreshPttPorts();

        _modem.SetManualPromptHandler(SetManualPttPrompt);
        _modem.Changed += OnModemChanged;
        ((INotifyCollectionChanged)_modem.Decodes).CollectionChanged += OnDecodesChanged;

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _uiTimer.Tick += (_, _) => RefreshUiTick();
        SlotPeriodLabel = _l.Get("Ft4.Slot.Rx");
        SlotProgressText = "0%";
    }

    public ObservableCollection<Ft4DecodedMessage> Decodes { get; } = [];

    public ObservableCollection<Ft4AudioDeviceOption> InputDeviceOptions { get; } = [];

    public ObservableCollection<Ft4AudioDeviceOption> OutputDeviceOptions { get; } = [];

    public ObservableCollection<string> PttPortOptions { get; } = [];

    public IReadOnlyList<Ft4PttMethodOption> PttMethodOptions { get; }

    public IReadOnlyList<Ft4PttLineOption> PttLineOptions { get; }

    public string StationCallsign =>
        Ft4MessageCodec.NormalizeCall(_settings.Current.GroundStation.Callsign ?? "");

    public string StationGrid =>
        (_settings.Current.GroundStation.GridSquare ?? "").Trim().ToUpperInvariant();

    /// <summary>Example CQ using Settings → Station (not the old CALL/GRID placeholders).</summary>
    public string TxMessageWatermark
    {
        get
        {
            var call = StationCallsign;
            var grid = StationGrid;
            if (call.Length == 0 || grid.Length < 4)
                return _l.Get("Ft4.TxMessage.Watermark");
            return Ft4MessageCodec.BuildCq(call, grid[..4]);
        }
    }

    public bool ShowHandshakePttOptions =>
        SelectedPttMethod?.Value is Ft4PttMethod.CatPortHandshake or Ft4PttMethod.SeparateComPort;

    public bool ShowSeparatePttPort =>
        SelectedPttMethod?.Value == Ft4PttMethod.SeparateComPort;

    public bool HasDoppler =>
        !string.IsNullOrWhiteSpace(DopplerUplinkText) && DopplerUplinkText != "-"
        || !string.IsNullOrWhiteSpace(DopplerDownlinkText) && DopplerDownlinkText != "-";

    [ObservableProperty] private string _waterfallStatusText = "";
    [ObservableProperty] private string _slotClockText = "";
    [ObservableProperty] private string _slotPeriodLabel = "";
    [ObservableProperty] private string _slotProgressText = "";
    [ObservableProperty] private double _slotProgressPercent;
    [ObservableProperty] private bool _isTxSlot;
    [ObservableProperty] private string _dopplerUplinkText = "";
    [ObservableProperty] private string _dopplerDownlinkText = "";
    [ObservableProperty] private string _currentTxMessage = "";
    [ObservableProperty] private string _manualPttPrompt = "";
    [ObservableProperty] private string _statusLine = "";
    [ObservableProperty] private bool _skipRrr;
    [ObservableProperty] private bool _preferEvenSlot;
    [ObservableProperty] private bool _holdTxFrequency = true;
    [ObservableProperty] private bool _audioDopplerTx = true;
    [ObservableProperty] private bool _audioDopplerRx = true;
    [ObservableProperty] private int _pttLeadMs = 200;
    [ObservableProperty] private int _pttTailMs = 100;
    [ObservableProperty] private double _decodeFontSize = 12;
    [ObservableProperty] private double _txAudioHz = 1500;
    [ObservableProperty] private double _txLevel = 0.35;
    [ObservableProperty] private bool _txEnabled;
    [ObservableProperty] private bool _pttInvert;
    [ObservableProperty] private string _separatePttPort = "";
    [ObservableProperty] private Ft4PttMethodOption? _selectedPttMethod;
    [ObservableProperty] private Ft4PttLineOption? _selectedPttLine;
    [ObservableProperty] private Ft4AudioDeviceOption? _selectedInputDevice;
    [ObservableProperty] private Ft4AudioDeviceOption? _selectedOutputDevice;
    [ObservableProperty] private Ft4DecodedMessage? _selectedDecode;
    [ObservableProperty] private float[]? _spectrumBins;

    partial void OnSkipRrrChanged(bool value)
    {
        _settings.Current.Ft4.SkipRrr = value;
        _settings.RequestSave();
    }

    partial void OnHoldTxFrequencyChanged(bool value)
    {
        _settings.Current.Ft4.HoldTxFrequency = value;
        _settings.RequestSave();
    }

    partial void OnAudioDopplerTxChanged(bool value)
    {
        _settings.Current.Ft4.AudioDopplerTx = value;
        _settings.RequestSave();
    }

    partial void OnAudioDopplerRxChanged(bool value)
    {
        _settings.Current.Ft4.AudioDopplerRx = value;
        _settings.RequestSave();
    }

    partial void OnPttLeadMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2000);
        if (clamped != value)
        {
            PttLeadMs = clamped;
            return;
        }

        _settings.Current.Ft4.PttLeadMs = clamped;
        _settings.RequestSave();
    }

    partial void OnPttTailMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2000);
        if (clamped != value)
        {
            PttTailMs = clamped;
            return;
        }

        _settings.Current.Ft4.PttTailMs = clamped;
        _settings.RequestSave();
    }

    partial void OnDecodeFontSizeChanged(double value)
    {
        var clamped = Math.Clamp(value, 10, 28);
        if (Math.Abs(clamped - value) > 0.01)
        {
            DecodeFontSize = clamped;
            return;
        }

        _settings.Current.Ft4.DecodeFontSize = clamped;
        _settings.RequestSave();
    }

    partial void OnTxAudioHzChanged(double value)
    {
        var clamped = Math.Clamp(value, 200, 3000);
        if (Math.Abs(clamped - value) > 0.01)
        {
            TxAudioHz = clamped;
            return;
        }

        _settings.Current.Ft4.TxAudioHz = clamped;
        if (_modem.Sequencer is not null)
            _modem.Sequencer.TxAudioHz = clamped;
        _settings.RequestSave();
    }

    partial void OnTxLevelChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.05, 1.0);
        if (Math.Abs(clamped - value) > 0.0001)
        {
            TxLevel = clamped;
            return;
        }

        _settings.Current.Ft4.TxLevel = clamped;
        _settings.RequestSave();
    }

    partial void OnPreferEvenSlotChanged(bool value)
    {
        if (_modem.Sequencer is not null)
            _modem.Sequencer.PreferEvenSlot = value;
    }

    partial void OnSelectedPttMethodChanged(Ft4PttMethodOption? value)
    {
        if (value is null)
            return;
        _settings.Current.Ft4.PttMethod = value.Value;
        _settings.RequestSave();
        OnPropertyChanged(nameof(ShowHandshakePttOptions));
        OnPropertyChanged(nameof(ShowSeparatePttPort));
    }

    partial void OnSelectedPttLineChanged(Ft4PttLineOption? value)
    {
        if (value is null)
            return;
        _settings.Current.Ft4.PttLine = value.Value;
        _settings.RequestSave();
    }

    partial void OnPttInvertChanged(bool value)
    {
        _settings.Current.Ft4.PttInvert = value;
        _settings.RequestSave();
    }

    partial void OnSeparatePttPortChanged(string value)
    {
        _settings.Current.Ft4.SeparatePttPort = value?.Trim() ?? "";
        _settings.RequestSave();
    }

    partial void OnSelectedInputDeviceChanged(Ft4AudioDeviceOption? value)
    {
        if (_loadingDevices || value is null)
            return;
        _settings.Current.Ft4.InputDeviceId = value.Id;
        _settings.Current.Ft4.InputDeviceDisplayName = value.DisplayName;
        _settings.RequestSave();
        _modem.RestartCaptureFromSettings();
    }

    partial void OnSelectedOutputDeviceChanged(Ft4AudioDeviceOption? value)
    {
        if (_loadingDevices || value is null)
            return;
        _settings.Current.Ft4.OutputDeviceId = value.Id;
        _settings.Current.Ft4.OutputDeviceDisplayName = value.DisplayName;
        _settings.RequestSave();
    }

    partial void OnSelectedDecodeChanged(Ft4DecodedMessage? value)
    {
        if (value is null)
            return;
        AnswerDecode(value);
    }

    public Task OnWindowOpenedAsync()
    {
        RefreshAudioDevices();
        RefreshPttPorts();
        _uiTimer.Start();
        RefreshUiTick();
        if (!_modem.IsRunning)
            StartSession();
        return Task.CompletedTask;
    }

    public Task OnWindowClosedAsync()
    {
        _uiTimer.Stop();
        return Task.CompletedTask;
    }

    public void StartSession()
    {
        if (!_modem.NativeAvailable)
        {
            StatusLine = _l.Get("Ft4.NativeUnavailable");
            WaterfallStatusText = _l.Get("Ft4.Waterfall.Unavailable");
            Log.Warning("FT4 native library unavailable");
            return;
        }

        if (StationCallsign.Length == 0 || StationGrid.Length < 4)
        {
            StatusLine = _l.Get("Ft4.Status.NeedStation");
            return;
        }

        _modem.Start();
        StatusLine = string.IsNullOrWhiteSpace(_modem.Status)
            ? _l.Get("Ft4.Status.Ready")
            : _modem.Status;
        WaterfallStatusText = _l.Get("Ft4.Waterfall.Listening");
    }

    public async Task StopSessionAsync()
    {
        await _modem.StopAsync().ConfigureAwait(true);
        TxEnabled = false;
        StatusLine = _l.Get("Ft4.Status.Idle");
        WaterfallStatusText = _l.Get("Ft4.Waterfall.Unavailable");
    }

    [RelayCommand(CanExecute = nameof(CanEnableTx))]
    private void EnableTx()
    {
        PushTxMessageToModem();
        _modem.EnableTx();
        TxEnabled = _modem.Sequencer?.TransmitEnabled == true;
        CurrentTxMessage = _modem.Sequencer?.CurrentTxMessage ?? CurrentTxMessage;
        StatusLine = _l.Get("Ft4.Status.TxEnabled");
    }

    private bool CanEnableTx() => !TxEnabled;

    [RelayCommand(CanExecute = nameof(CanHaltTx))]
    private void HaltTx()
    {
        _modem.HaltTx();
        TxEnabled = false;
        ManualPttPrompt = "";
        StatusLine = _l.Get("Ft4.Status.TxHalted");
    }

    private bool CanHaltTx() => TxEnabled;

    partial void OnTxEnabledChanged(bool value)
    {
        EnableTxCommand.NotifyCanExecuteChanged();
        HaltTxCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void StartCq()
    {
        // Rebuild from Settings → Station so portable callsigns (e.g. MM9SQL/M) pack correctly.
        _modem.StartCq(PreferEvenSlot);
        CurrentTxMessage = _modem.Sequencer?.CurrentTxMessage ?? "";
        TxEnabled = _modem.Sequencer?.TransmitEnabled == true;
        StatusLine = _l.Get("Ft4.Status.CallingCq");
        OnPropertyChanged(nameof(CanManualLog));
        ManualLogCommand.NotifyCanExecuteChanged();
    }

    private void PushTxMessageToModem()
    {
        var text = (CurrentTxMessage ?? "").Trim();
        if (text.Length == 0 || IsPlaceholderTxMessage(text))
            return;

        _modem.Sequencer?.SetTxMessage(text);
    }

    private bool IsPlaceholderTxMessage(string text) =>
        text.Equals("CQ CALL GRID", StringComparison.OrdinalIgnoreCase)
        || text.Equals(TxMessageWatermark, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void AnswerDecode(Ft4DecodedMessage? decode)
    {
        if (decode is null || decode.IsTransmitted || decode.IsOwnEcho)
            return;
        _modem.Answer(decode);
        CurrentTxMessage = _modem.Sequencer?.CurrentTxMessage ?? "";
        TxEnabled = _modem.Sequencer?.TransmitEnabled == true;
        PreferEvenSlot = _modem.Sequencer?.PreferEvenSlot ?? PreferEvenSlot;
        if (!_settings.Current.Ft4.HoldTxFrequency)
            TxAudioHz = decode.FreqHz;
        else if (_modem.Sequencer is not null)
            TxAudioHz = _modem.Sequencer.TxAudioHz;
        StatusLine = _l.Get("Ft4.Status.Answering", decode.Text);
        OnPropertyChanged(nameof(CanManualLog));
        ManualLogCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearDecodes()
    {
        _modem.ClearDecodes();
        Decodes.Clear();
        StatusLine = _l.Get("Ft4.Status.DecodesCleared");
    }

    [RelayCommand]
    private async Task SaveActivityAsync()
    {
        if (Decodes.Count == 0)
        {
            StatusLine = _l.Get("Ft4.Status.NoActivity");
            return;
        }

        var owner = App.MainWindow;
        var storage = Avalonia.Controls.TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (storage is null)
        {
            StatusLine = _l.Get("Ft4.Status.SaveActivityFailed", "Storage unavailable.");
            return;
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = _l.Get("Ft4.SaveActivity.Title"),
            SuggestedFileName = $"OscarWatch-FT4-{stamp}.txt",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new Avalonia.Platform.Storage.FilePickerFileType(_l.Get("Ft4.SaveActivity.FileType"))
                {
                    Patterns = ["*.txt"],
                    MimeTypes = ["text/plain"]
                }
            ]
        }).ConfigureAwait(true);

        if (file is null)
            return;

        try
        {
            var text = _modem.BuildActivityText();
            await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(text).ConfigureAwait(true);
            StatusLine = _l.Get("Ft4.Status.ActivitySaved", file.Name);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "FT4 save activity failed");
            StatusLine = _l.Get("Ft4.Status.SaveActivityFailed", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanManualLog))]
    private async Task ManualLogAsync()
    {
        await _modem.LogQsoAsync(manual: true).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(_modem.Status))
            StatusLine = _modem.Status;
        else if (_modem.Sequencer?.TheirCall is not null)
            StatusLine = _l.Get("Ft4.Logged", _modem.Sequencer.TheirCall);
        ManualLogCommand.NotifyCanExecuteChanged();
    }

    public bool CanManualLog =>
        _modem.Sequencer?.TheirCall is not null
        && _modem.Sequencer.Phase is Ft4QsoPhase.InQso or Ft4QsoPhase.Finished;

    [RelayCommand]
    private void RefreshDevices()
    {
        RefreshAudioDevices();
        RefreshPttPorts();
    }

    public void SetManualPttPrompt(string phase)
    {
        ManualPttPrompt = phase switch
        {
            "key" => _l.Get("Ft4.ManualKey"),
            "unkey" => _l.Get("Ft4.ManualUnkey"),
            _ => phase
        };
    }

    private void RefreshAudioDevices()
    {
        _loadingDevices = true;
        try
        {
            var defaultLabel = _l.Get("Ft4.Audio.SystemDefault");
            var savedIn = _settings.Current.Ft4.InputDeviceId ?? "";
            var savedOut = _settings.Current.Ft4.OutputDeviceId ?? "";

            InputDeviceOptions.Clear();
            InputDeviceOptions.Add(new Ft4AudioDeviceOption("", defaultLabel));
            foreach (var d in _modem.GetInputDevices())
                InputDeviceOptions.Add(new Ft4AudioDeviceOption(d.Id, d.DisplayName));

            OutputDeviceOptions.Clear();
            OutputDeviceOptions.Add(new Ft4AudioDeviceOption("", defaultLabel));
            foreach (var d in _modem.GetOutputDevices())
                OutputDeviceOptions.Add(new Ft4AudioDeviceOption(d.Id, d.DisplayName));

            SelectedInputDevice = InputDeviceOptions.FirstOrDefault(o => o.Id == savedIn)
                ?? InputDeviceOptions[0];
            SelectedOutputDevice = OutputDeviceOptions.FirstOrDefault(o => o.Id == savedOut)
                ?? OutputDeviceOptions[0];
        }
        finally
        {
            _loadingDevices = false;
        }
    }

    private void RefreshPttPorts()
    {
        var saved = _settings.Current.Ft4.SeparatePttPort?.Trim() ?? "";
        PttPortOptions.Clear();
        foreach (var port in SerialPortDiscovery.GetAvailablePorts(forceRefresh: true))
            PttPortOptions.Add(port);
        if (!string.IsNullOrEmpty(saved)
            && !PttPortOptions.Contains(saved, StringComparer.OrdinalIgnoreCase))
            PttPortOptions.Insert(0, saved);
        SeparatePttPort = saved;
    }

    private void OnModemChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!string.IsNullOrWhiteSpace(_modem.Status))
                StatusLine = _modem.Status;
            CurrentTxMessage = _modem.Sequencer?.CurrentTxMessage ?? CurrentTxMessage;
            TxEnabled = _modem.Sequencer?.TransmitEnabled == true;
            if (!string.IsNullOrEmpty(_modem.ManualPrompt))
                SetManualPttPrompt(_modem.ManualPrompt);
            OnPropertyChanged(nameof(CanManualLog));
            ManualLogCommand.NotifyCanExecuteChanged();
        });
    }

    private void OnDecodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Decodes.Clear();
            foreach (var d in _modem.Decodes)
                Decodes.Add(d);
        });
    }

    private void RefreshUiTick()
    {
        var utc = DateTime.UtcNow;
        var slotStart = Ft4SlotClock.SlotStartUtc(utc, Ft4SlotClock.Ft4SlotSeconds);
        var into = Ft4SlotClock.SecondsIntoSlot(utc, Ft4SlotClock.Ft4SlotSeconds);
        var even = Ft4SlotClock.IsEvenSlot(slotStart, Ft4SlotClock.Ft4SlotSeconds);
        SlotClockText = $"{slotStart:HH:mm:ss.f} UTC  +{into:0.0}s  {(even ? "even" : "odd")}";

        var progress = Math.Clamp(100.0 * into / Ft4SlotClock.Ft4SlotSeconds, 0, 100);
        SlotProgressPercent = progress;
        SlotProgressText = $"{progress:0}%";
        var preferEven = _modem.Sequencer?.PreferEvenSlot ?? PreferEvenSlot;
        IsTxSlot = TxEnabled && even == preferEven;
        SlotPeriodLabel = IsTxSlot ? _l.Get("Ft4.Slot.Tx") : _l.Get("Ft4.Slot.Rx");

        OnPropertyChanged(nameof(TxMessageWatermark));

        DopplerUplinkText = string.IsNullOrWhiteSpace(_frequencyOverlay.RadioTransmitText)
            ? "-"
            : _frequencyOverlay.RadioTransmitText;
        DopplerDownlinkText = string.IsNullOrWhiteSpace(_frequencyOverlay.RadioReceiveText)
            ? "-"
            : _frequencyOverlay.RadioReceiveText;
        OnPropertyChanged(nameof(HasDoppler));

        if (_modem.IsRunning)
        {
            var snap = _tracker.GetCurrent();
            var sat = string.IsNullOrWhiteSpace(snap.SatelliteName) ? null : snap.SatelliteName;
            WaterfallStatusText = sat is null
                ? _l.Get("Ft4.Waterfall.Listening")
                : _l.Get("Ft4.Waterfall.ListeningSat", sat);

            var bins = new float[Ft4WaterfallControl.SpectrumColumns];
            if (_modem.TryBuildSpectrum(bins))
                SpectrumBins = bins;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _uiTimer.Stop();
        _modem.Changed -= OnModemChanged;
        ((INotifyCollectionChanged)_modem.Decodes).CollectionChanged -= OnDecodesChanged;
        _ = StopSessionAsync();
    }
}

public sealed record Ft4PttMethodOption(Ft4PttMethod Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record Ft4PttLineOption(Ft4PttLine Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record Ft4AudioDeviceOption(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}
