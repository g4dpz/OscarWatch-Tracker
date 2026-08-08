using OscarWatch.Core.Models;
using OscarWatch.Rotator;

namespace OscarWatch.Tests;

internal sealed class RecordingRotatorDriver : IRotatorDriver
{
    public List<double> AzimuthHistory { get; } = [];

    public double? LastAzimuthDeg { get; private set; }
    public double? LastElevationDeg { get; private set; }
    public int SetPositionCallCount { get; private set; }
    public int GetPositionCallCount { get; private set; }
    public int StopCallCount { get; private set; }
    public int OpenCallCount { get; private set; }
    public int DisposeCallCount { get; private set; }

    /// <summary>When set, <see cref="GetPosition"/> returns this az instead of last commanded.</summary>
    public int? PolledAzimuthOverride { get; set; }

    public void Open() => OpenCallCount++;

    public void SetPosition(double azimuthDeg, double elevationDeg, RotatorSettings settings)
    {
        SetPositionCallCount++;
        LastAzimuthDeg = azimuthDeg;
        LastElevationDeg = elevationDeg;
        AzimuthHistory.Add(azimuthDeg);
    }

    public void Stop() => StopCallCount++;

    public (int? Azimuth, int? Elevation) GetPosition()
    {
        GetPositionCallCount++;
        if (PolledAzimuthOverride is { } polledAz && LastElevationDeg is { } el)
            return (polledAz, (int)Math.Round(el));

        return LastAzimuthDeg is { } az && LastElevationDeg is { } elevation
            ? ((int?)Math.Round(az), (int?)Math.Round(elevation))
            : (null, null);
    }

    public void Dispose() => DisposeCallCount++;
}
