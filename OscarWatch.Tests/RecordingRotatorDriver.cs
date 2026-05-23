using OscarWatch.Core.Models;
using OscarWatch.Rotator;

namespace OscarWatch.Tests;

internal sealed class RecordingRotatorDriver : IRotatorDriver
{
    public double? LastAzimuthDeg { get; private set; }
    public double? LastElevationDeg { get; private set; }
    public int SetPositionCallCount { get; private set; }
    public int GetPositionCallCount { get; private set; }

    public void Open() { }

    public void SetPosition(double azimuthDeg, double elevationDeg, RotatorSettings settings)
    {
        SetPositionCallCount++;
        LastAzimuthDeg = azimuthDeg;
        LastElevationDeg = elevationDeg;
    }

    public void Stop() { }

    public (int? Azimuth, int? Elevation) GetPosition()
    {
        GetPositionCallCount++;
        return LastAzimuthDeg is { } az && LastElevationDeg is { } el
            ? ((int?)Math.Round(az), (int?)Math.Round(el))
            : (null, null);
    }

    public void Dispose() { }
}
