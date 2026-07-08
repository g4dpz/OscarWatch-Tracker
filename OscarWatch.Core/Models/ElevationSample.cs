namespace OscarWatch.Core.Models;

/// <summary>
/// A single elevation sample for a satellite pass, expressing the time as minutes from
/// the current reference moment and the elevation in degrees (0–90).
/// </summary>
public readonly record struct ElevationSample(double MinutesFromNow, double ElevationDeg);
