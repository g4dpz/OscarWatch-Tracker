namespace OscarWatch.Core.Radio;

/// <summary>Optional inputs for linear predictive Doppler (look-ahead range rate).</summary>
public readonly record struct DopplerComputeOptions(
    bool PredictiveLinear,
    double RangeRateProbeKmPerSec);
