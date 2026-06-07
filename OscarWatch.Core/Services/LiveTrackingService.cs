using System.Collections.Concurrent;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Propagates enabled satellites on a dedicated thread; the UI reads snapshots without blocking.
/// </summary>
public sealed class LiveTrackingService : ILiveTrackingService
{
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CommandWaitTimeout = TimeSpan.FromSeconds(10);

    private readonly TrackingOrchestrator _orchestrator;
    private readonly Func<DateTime, IReadOnlyList<SatelliteTrackState>>? _computeOverride;
    private readonly object _snapshotLock = new();
    private readonly object _workerStartLock = new();

    private BlockingCollection<LiveTrackingCommand>? _commands;
    private Thread? _worker;
    private int _disposed;
    private volatile bool _shutdownRequested;

    private IReadOnlyList<SatelliteTrackState> _snapshot = Array.Empty<SatelliteTrackState>();
    private DateTime _snapshotUtc = DateTime.MinValue;
    private long _mapTimeOffsetTicks;

    public TimeSpan MapTimeOffset
    {
        get => new(Interlocked.Read(ref _mapTimeOffsetTicks));
        set => Interlocked.Exchange(ref _mapTimeOffsetTicks, value.Ticks);
    }

    public LiveTrackingService(TrackingOrchestrator orchestrator)
        : this(orchestrator, computeOverride: null)
    {
    }

    internal LiveTrackingService(
        TrackingOrchestrator orchestrator,
        Func<DateTime, IReadOnlyList<SatelliteTrackState>>? computeOverride)
    {
        _orchestrator = orchestrator;
        _computeOverride = computeOverride;
    }

    public DateTime SnapshotUtc
    {
        get { lock (_snapshotLock) return _snapshotUtc; }
    }

    public IReadOnlyList<SatelliteTrackState> GetSnapshot()
    {
        lock (_snapshotLock)
            return _snapshot;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        EnsureWorker();
    }

    public void RequestReload() =>
        Enqueue(new LiveTrackingCommand(LiveTrackingCommandKind.Reload));

    /// <summary>Blocks until queued commands are processed (unit tests).</summary>
    internal void DrainCommandQueueForTests() =>
        EnqueueAndWait(new LiveTrackingCommand(LiveTrackingCommandKind.Drain));

    /// <summary>Runs one propagation tick on the worker (unit tests).</summary>
    internal void RefreshSnapshotSynchronously() =>
        EnqueueAndWait(new LiveTrackingCommand(LiveTrackingCommandKind.RefreshSynchronously));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _shutdownRequested = true;
        try
        {
            if (_commands is not null)
            {
                try
                {
                    _commands.Add(new LiveTrackingCommand(LiveTrackingCommandKind.Shutdown));
                }
                catch (InvalidOperationException)
                {
                    // collection completed
                }
            }
        }
        catch
        {
            // best effort
        }

        _worker?.Join(TimeSpan.FromSeconds(2));
        _commands?.Dispose();
        _commands = null;
    }

    private void Enqueue(LiveTrackingCommand command)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        EnsureWorker();
        _commands!.Add(command);
    }

    private void EnqueueAndWait(LiveTrackingCommand command, TimeSpan? timeout = null)
    {
        using var done = new ManualResetEventSlim(false);
        command.Completed = done;
        Enqueue(command);
        if (!done.Wait(timeout ?? CommandWaitTimeout))
            throw new TimeoutException("Live tracking worker did not complete the command in time.");
    }

    private void EnsureWorker()
    {
        lock (_workerStartLock)
        {
            if (_worker is { IsAlive: true })
                return;

            _shutdownRequested = false;
            _commands = new BlockingCollection<LiveTrackingCommand>();
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "OscarWatch.Tracking"
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
                var commands = _commands;
                if (commands is null)
                    break;

                if (commands.TryTake(out var command, LoopInterval))
                {
                    ProcessCommand(command);
                    DrainPendingCommands();
                }

                if (_shutdownRequested)
                    break;

                RefreshSnapshot();
            }
        }
        finally
        {
            lock (_snapshotLock)
            {
                _snapshot = Array.Empty<SatelliteTrackState>();
                _snapshotUtc = DateTime.MinValue;
            }
        }
    }

    private void DrainPendingCommands()
    {
        var commands = _commands;
        if (commands is null)
            return;

        while (commands.TryTake(out var command, 0))
            ProcessCommand(command);
    }

    private void ProcessCommand(LiveTrackingCommand command)
    {
        try
        {
            switch (command.Kind)
            {
                case LiveTrackingCommandKind.Reload:
                    // Reload invalidates the cached enabled-satellite list (Req 2.4)
                    _orchestrator.ReloadEnabledSatellites();
                    RefreshSnapshot();
                    break;

                case LiveTrackingCommandKind.RefreshSynchronously:
                    RefreshSnapshot();
                    break;

                case LiveTrackingCommandKind.Drain:
                    break;

                case LiveTrackingCommandKind.Shutdown:
                    _shutdownRequested = true;
                    break;
            }
        }
        finally
        {
            command.Completed?.Set();
        }
    }

    private void RefreshSnapshot()
    {
        var utc = DateTime.UtcNow + MapTimeOffset;
        var states = _computeOverride?.Invoke(utc) ?? _orchestrator.GetLiveStates(utc);
        lock (_snapshotLock)
        {
            _snapshot = states;
            _snapshotUtc = utc;
        }
    }

    private enum LiveTrackingCommandKind
    {
        Reload,
        RefreshSynchronously,
        Drain,
        Shutdown
    }

    private sealed class LiveTrackingCommand
    {
        public LiveTrackingCommand(LiveTrackingCommandKind kind) => Kind = kind;

        public LiveTrackingCommandKind Kind { get; }
        public ManualResetEventSlim? Completed { get; set; }
    }
}
