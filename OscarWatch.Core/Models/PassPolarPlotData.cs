namespace OscarWatch.Core.Models;

public enum PassPolarPlotMarkerKind
{
    MutualWindowStart,
    MutualWindowEnd
}

public sealed class PassPolarPlotMarker
{
    public double AzimuthDeg { get; init; }
    public double ElevationDeg { get; init; }
    public PassPolarPlotMarkerKind Kind { get; init; }
}

public sealed class PassPolarPlotSegment
{
    public required bool IsSunlit { get; init; }
    public required IReadOnlyList<(double AzimuthDeg, double ElevationDeg)> Points { get; init; }
}

public sealed class PassPolarPlotData
{
    public required string StationLabel { get; init; }
    public double AosAzimuthDeg { get; init; }
    public double LosAzimuthDeg { get; init; }
    public double MaxElevationDeg { get; init; }
    public required IReadOnlyList<PassPolarPlotSegment> Segments { get; init; }
    public PassPolarPlotMarker? MutualStart { get; init; }
    public PassPolarPlotMarker? MutualEnd { get; init; }
}
