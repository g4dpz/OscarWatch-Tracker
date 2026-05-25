using System.Collections.Concurrent;
using OscarWatch.Core.Models;
using OscarWatch.Core.Rotator;
using OscarWatch.Core.Services;
using Serilog;

namespace OscarWatch.Rotator;

/// <summary>
/// All rotator I/O runs on a dedicated background thread; the UI only enqueues commands and reads status.
/// </summary>
public sealed class RotatorController : IRotatorController, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<RotatorController>();
    private static readonly TimeSpan LoopInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CommandWaitTimeout = TimeSpan.FromSeconds(10);

    private readonly Func<RotatorSettings, IRotatorDriver>? _driverFactory;
    private readonly object _statusLock = new();
    private readonly object _workerStartLock = new();

    private BlockingCollection<RotatorCommand>? _commands;
    private Thread? _worker;
    private int _disposed;
    private volatile bool _shutdownRequested;

    private IRotatorDriver? _rotator;
    private string? _connectedPort;
    private int _connectedBaudRate;
    private RotatorType _connectedType;
    private string? _lastTargetNoradId;
    private double? _lastAzimuth;
    private double? _lastElevation;
    private bool _parked;
    private bool _manualParkActive;
    private bool _standbyActive;
    private int? _displayAzimuth;
    private int? _displayElevation;
    private int? _displayCommandedAzimuth;
    private int? _displayCompassAzimuth;

    private RotatorSettings _cachedSettings = new();
    private SatelliteTrackState? _cachedTarget;
    private RotatorPositionStatus _positionStatus = new(false, null, null);

    public RotatorController(Func<RotatorSettings, IRotatorDriver>? driverFactory = null) =>
        _driverFactory = driverFactory;

    public RotatorPositionStatus GetPositionStatus()
    {
        lock (_statusLock)
            return _positionStatus;
    }

    /// <summary>Enqueue latest pass/settings for the rotator thread (~1 Hz from UI).</summary>
    public void Update(RotatorSettings settings, SatelliteTrackState? target) =>
        Enqueue(new RotatorCommand(RotatorCommandKind.PublishTarget, settings, target));

    public void Park(RotatorSettings settings) =>
        Enqueue(new RotatorCommand(RotatorCommandKind.Park, settings));

    public void SetStandby(bool active, RotatorSettings settings) =>
        Enqueue(new RotatorCommand(RotatorCommandKind.SetStandby, settings, standbyActive: active));

    public void Disconnect() =>
        Enqueue(new RotatorCommand(RotatorCommandKind.Disconnect));

    /// <summary>Synchronous tracking tick (unit tests).</summary>
    internal void UpdateSynchronously(RotatorSettings settings, SatelliteTrackState? target) =>
        EnqueueAndWait(new RotatorCommand(RotatorCommandKind.UpdateSynchronously, settings, target));

    /// <summary>Blocks until queued commands are processed (unit tests).</summary>
    internal void DrainCommandQueueForTests() =>
        EnqueueAndWait(new RotatorCommand(RotatorCommandKind.Drain));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        try
        {
            if (_commands is not null && _worker is { IsAlive: true })
                EnqueueAndWait(new RotatorCommand(RotatorCommandKind.Shutdown), TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Rotator worker shutdown did not complete cleanly");
        }

        _commands?.Dispose();
        _commands = null;
        _worker?.Join(TimeSpan.FromSeconds(2));
    }

    private void Enqueue(RotatorCommand command)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        EnsureWorker();
        _commands!.Add(command);
    }

    private void EnqueueAndWait(RotatorCommand command, TimeSpan? timeout = null)
    {
        using var done = new ManualResetEventSlim(false);
        command.Completed = done;
        Enqueue(command);
        if (!done.Wait(timeout ?? CommandWaitTimeout))
            throw new TimeoutException("Rotator worker did not complete the command in time.");
    }

    private void EnsureWorker()
    {
        lock (_workerStartLock)
        {
            if (_worker is { IsAlive: true })
                return;

            _shutdownRequested = false;
            _commands = new BlockingCollection<RotatorCommand>();
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "OscarWatch.Rotator"
            };
            _worker.Start();
        }
    }

    private void WorkerLoop()
    {
        try
        {
            while (!_shutdownRequested)
            {
                if (_commands!.TryTake(out var command, LoopInterval))
                {
                    ProcessCommand(command);
                    DrainPendingCommands();
                }

                if (_shutdownRequested)
                    break;

                RunTrackingIteration();
                RefreshPositionSnapshot();
            }
        }
        finally
        {
            TearDownRotator();
            RefreshPositionSnapshot();
        }
    }

    private void DrainPendingCommands()
    {
        while (_commands!.TryTake(out var command, 0))
            ProcessCommand(command);
    }

    private void ProcessCommand(RotatorCommand command)
    {
        try
        {
            switch (command.Kind)
            {
                case RotatorCommandKind.PublishTarget:
                    _cachedSettings = command.Settings;
                    _cachedTarget = command.Target;
                    break;

                case RotatorCommandKind.UpdateSynchronously:
                    _cachedSettings = command.Settings;
                    _cachedTarget = command.Target;
                    RunTrackingIteration();
                    break;

                case RotatorCommandKind.Park:
                    ParkOnWorker(command.Settings);
                    break;

                case RotatorCommandKind.SetStandby:
                    SetStandbyOnWorker(command.StandbyActive!.Value, command.Settings);
                    break;

                case RotatorCommandKind.Disconnect:
                    TearDownRotator();
                    ResetTrackingState();
                    break;

                case RotatorCommandKind.Drain:
                    break;

                case RotatorCommandKind.Shutdown:
                    _shutdownRequested = true;
                    break;
            }
        }
        finally
        {
            RefreshPositionSnapshot();
            command.Completed?.Set();
        }
    }

    private void RunTrackingIteration()
    {
        var settings = _cachedSettings;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Port))
        {
            TearDownRotator();
            ResetTrackingState();
            return;
        }

        if (!EnsureConnected(settings))
            return;

        PollPosition();

        if (_standbyActive)
        {
            TryPark(settings);
            return;
        }

        var target = _cachedTarget;
        if (target?.NoradId != _lastTargetNoradId)
        {
            _lastTargetNoradId = target?.NoradId;
            _lastAzimuth = null;
            _lastElevation = null;
            _parked = false;
            ClearTrackingAzimuthDisplay();
        }

        if (_manualParkActive)
        {
            if (target?.LookAngles is { } look && look.ElevationDeg >= settings.TrackStartElevationDeg)
            {
                TryPark(settings);
                return;
            }

            _manualParkActive = false;
            _parked = false;
        }

        if (target?.LookAngles is { } lookAngles)
        {
            if (lookAngles.ElevationDeg >= settings.TrackStartElevationDeg)
                TryTrack(settings, lookAngles.AzimuthDeg, lookAngles.ElevationDeg, target.AheadAzimuthDeg);
            else
                TryPark(settings);
        }
        else
            TryPark(settings);
    }

    private void ParkOnWorker(RotatorSettings settings)
    {
        if (_standbyActive)
            return;

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Port))
            return;

        if (!EnsureConnected(settings))
            return;

        _manualParkActive = true;
        _parked = false;
        TryPark(settings);
        PollPosition();
    }

    private void SetStandbyOnWorker(bool active, RotatorSettings settings)
    {
        _standbyActive = active;

        if (!active)
        {
            _lastTargetNoradId = null;
            _lastAzimuth = null;
            _lastElevation = null;
            _parked = false;
            _manualParkActive = false;
            return;
        }

        _manualParkActive = false;

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Port))
            return;

        if (!EnsureConnected(settings))
            return;

        _parked = false;
        TryPark(settings);
        PollPosition();
    }

    private void TearDownRotator()
    {
        _rotator?.Dispose();
        _rotator = null;
        _connectedPort = null;
        _connectedBaudRate = 0;
        _connectedType = default;
    }

    private void ResetTrackingState()
    {
        _lastTargetNoradId = null;
        _lastAzimuth = null;
        _lastElevation = null;
        _parked = false;
        _manualParkActive = false;
        _standbyActive = false;
        _displayAzimuth = null;
        _displayElevation = null;
        _displayCommandedAzimuth = null;
        _displayCompassAzimuth = null;
    }

    private void RefreshPositionSnapshot()
    {
        lock (_statusLock)
            _positionStatus = new(
                _rotator is not null,
                _displayAzimuth,
                _displayElevation,
                _displayCommandedAzimuth,
                _displayCompassAzimuth);
    }

    private void ClearTrackingAzimuthDisplay()
    {
        _displayCommandedAzimuth = null;
        _displayCompassAzimuth = null;
    }

    private bool EnsureConnected(RotatorSettings settings)
    {
        if (_rotator is not null
            && _connectedPort == settings.Port
            && _connectedBaudRate == settings.BaudRate
            && _connectedType == settings.Type)
            return true;

        TearDownRotator();

        try
        {
            _rotator = _driverFactory?.Invoke(settings) ?? RotatorDriverFactory.Create(settings);
            _rotator.Open();
            _connectedPort = settings.Port;
            _connectedBaudRate = settings.BaudRate;
            _connectedType = settings.Type;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Rotator connect failed on {Port}", settings.Port);
            TearDownRotator();
            return false;
        }
    }

    private void TryTrack(
        RotatorSettings settings,
        double azimuthDeg,
        double elevationDeg,
        double? aheadAzimuthDeg = null)
    {
        if (_rotator is null)
            return;

        var useSmartAzimuth = settings.SmartAzimuth450 && settings.MaxAzimuthDeg > 360;
        var effectiveLastAzimuth = _lastAzimuth ?? _displayAzimuth;
        var commandAz = useSmartAzimuth
            ? RotatorAzimuthPlanner.ResolveCommandAz(
                effectiveLastAzimuth, azimuthDeg, settings.MaxAzimuthDeg, aheadAzimuthDeg)
            : azimuthDeg;

        _displayCommandedAzimuth = (int)Math.Round(commandAz);
        _displayCompassAzimuth = (int)Math.Round(RotatorAzimuthPlanner.Normalize360(azimuthDeg));

        var send = _lastAzimuth is null || _lastElevation is null
            || Math.Abs(commandAz - _lastAzimuth.Value) >= 1
            || Math.Abs(elevationDeg - _lastElevation.Value) >= 1;

        if (!send)
            return;

        try
        {
            _rotator.SetPosition(commandAz, elevationDeg, settings);
            _lastAzimuth = Math.Round(commandAz);
            _lastElevation = Math.Round(elevationDeg);
            _parked = false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Rotator track failed at Az={Az} El={El}", commandAz, elevationDeg);
            TearDownRotator();
        }
    }

    private void TryPark(RotatorSettings settings)
    {
        if (_rotator is null || _parked)
            return;

        try
        {
            _rotator.SetPosition(settings.ParkAzimuthDeg, settings.ParkElevationDeg, settings);
            _lastAzimuth = settings.ParkAzimuthDeg;
            _lastElevation = settings.ParkElevationDeg;
            _parked = true;
            ClearTrackingAzimuthDisplay();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Rotator park failed");
            TearDownRotator();
        }
    }

    private void PollPosition()
    {
        if (_rotator is null)
            return;

        try
        {
            var (az, el) = _rotator.GetPosition();
            if (az is not null)
                _displayAzimuth = az;
            if (el is not null)
                _displayElevation = el;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Rotator position poll failed");
            TearDownRotator();
        }
    }

    private enum RotatorCommandKind
    {
        PublishTarget,
        UpdateSynchronously,
        Park,
        SetStandby,
        Disconnect,
        Drain,
        Shutdown
    }

    private sealed class RotatorCommand
    {
        public RotatorCommand(
            RotatorCommandKind kind,
            RotatorSettings? settings = null,
            SatelliteTrackState? target = null,
            bool? standbyActive = null)
        {
            Kind = kind;
            Settings = settings ?? new RotatorSettings();
            Target = target;
            StandbyActive = standbyActive;
        }

        public RotatorCommandKind Kind { get; }
        public RotatorSettings Settings { get; }
        public SatelliteTrackState? Target { get; }
        public bool? StandbyActive { get; }
        public ManualResetEventSlim? Completed { get; set; }
    }
}
