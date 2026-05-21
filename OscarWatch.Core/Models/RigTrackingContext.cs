namespace OscarWatch.Core.Models;

public sealed class RigTrackingContext
{
    public required SatelliteTrackState TrackState { get; init; }
    public required SatelliteTransponderMode Mode { get; init; }
    public required CorrectedFrequencies Corrected { get; init; }
    public double TransmitOffsetKHz { get; init; }
    public double ReceiveOffsetKHz { get; init; }
    public double? SelectedCtcssHz { get; init; }
}
