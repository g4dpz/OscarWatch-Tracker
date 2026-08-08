namespace OscarWatch.Core.Models;

/// <summary>Mechanical azimuth band for Smart 450° pass planning.</summary>
public enum SmartAzimuthBand
{
    /// <summary>Command compass azimuth in 0–360°.</summary>
    Primary = 0,

    /// <summary>Command compass azimuth + 360° (overlap / 361–450°).</summary>
    Extended = 1
}

/// <summary>
/// AOS–LOS Smart 450° plan: which mechanical band to use at each sample time.
/// Live tracking still uses current look angles; the plan only selects Primary vs Extended.
/// </summary>
public sealed record SmartAzimuthPassPlan(
    DateTime AosUtc,
    DateTime LosUtc,
    IReadOnlyList<SmartAzimuthPassSample> Samples);

public sealed record SmartAzimuthPassSample(
    DateTime Utc,
    SmartAzimuthBand Band);
