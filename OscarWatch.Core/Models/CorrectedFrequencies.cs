namespace OscarWatch.Core.Models;

public sealed record CorrectedFrequencies(
    double RadioTransmitKHz,
    double RadioReceiveKHz,
    double SatelliteTransmitKHz,
    double SatelliteReceiveKHz,
    double DopplerShiftKHz,
    bool IsBeaconOnly);
