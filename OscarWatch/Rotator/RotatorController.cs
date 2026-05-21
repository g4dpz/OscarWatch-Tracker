using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Rotator;

public sealed class RotatorController : IRotatorController, IDisposable
{
    private IRotatorDriver? _rotator;
    private string? _connectedPort;
    private int _connectedBaudRate;
    private RotatorType _connectedType;
    private string? _lastTargetNoradId;
    private double? _lastAzimuth;
    private double? _lastElevation;
    private bool _parked;
    private int? _displayAzimuth;
    private int? _displayElevation;

    public RotatorPositionStatus GetPositionStatus() =>
        new(_rotator is not null, _displayAzimuth, _displayElevation);

    public void Update(RotatorSettings settings, SatelliteTrackState? target)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Port))
        {
            Disconnect();
            return;
        }

        if (!EnsureConnected(settings))
            return;

        if (target?.NoradId != _lastTargetNoradId)
        {
            _lastTargetNoradId = target?.NoradId;
            _lastAzimuth = null;
            _lastElevation = null;
            _parked = false;
        }

        PollPosition();

        if (target?.LookAngles is { } look)
        {
            if (look.ElevationDeg >= settings.TrackStartElevationDeg)
                TryTrack(settings, look.AzimuthDeg, look.ElevationDeg);
            else
                TryPark(settings);
        }
        else
            TryPark(settings);
    }

    public void Disconnect()
    {
        _rotator?.Dispose();
        _rotator = null;
        _connectedPort = null;
        _connectedBaudRate = 0;
        _connectedType = default;
        _lastTargetNoradId = null;
        _lastAzimuth = null;
        _lastElevation = null;
        _parked = false;
        _displayAzimuth = null;
        _displayElevation = null;
    }

    public void Dispose() => Disconnect();

    private bool EnsureConnected(RotatorSettings settings)
    {
        if (_rotator is not null
            && _connectedPort == settings.Port
            && _connectedBaudRate == settings.BaudRate
            && _connectedType == settings.Type)
            return true;

        Disconnect();

        try
        {
            _rotator = RotatorDriverFactory.Create(settings);
            _rotator.Open();
            _connectedPort = settings.Port;
            _connectedBaudRate = settings.BaudRate;
            _connectedType = settings.Type;
            return true;
        }
        catch
        {
            Disconnect();
            return false;
        }
    }

    private void TryTrack(RotatorSettings settings, double azimuthDeg, double elevationDeg)
    {
        if (_rotator is null)
            return;

        var send = _lastAzimuth is null || _lastElevation is null
            || Math.Abs(azimuthDeg - _lastAzimuth.Value) >= 1
            || Math.Abs(elevationDeg - _lastElevation.Value) >= 1;

        if (!send)
            return;

        try
        {
            _rotator.SetPosition(azimuthDeg, elevationDeg, settings);
            _lastAzimuth = Math.Round(azimuthDeg);
            _lastElevation = Math.Round(elevationDeg);
            _parked = false;
        }
        catch
        {
            Disconnect();
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
        }
        catch
        {
            Disconnect();
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
        catch
        {
            Disconnect();
        }
    }
}
