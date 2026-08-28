namespace OscarWatch.Core.Models;

public sealed class SatelliteTrackState
{
    public required string Name { get; set; }
    public required string NoradId { get; set; }
    public required GeoCoordinate Subpoint { get; set; }
    public LookAngles? LookAngles { get; set; }
    /// <summary>Compass azimuth ~1–2 s ahead (rotator east-side north-wrap lookahead).</summary>
    public double? AheadAzimuthDeg { get; set; }
    /// <summary>Ground-track direction at the subpoint (degrees clockwise from north) for map footprint arrows.</summary>
    public double? MotionHeadingDeg { get; set; }
    public IReadOnlyList<GeoCoordinate> GroundTrack { get; set; } = Array.Empty<GeoCoordinate>();
    /// <summary>Ground track for the next orbit (one period ahead), used for the multi-track overlay.</summary>
    public IReadOnlyList<GeoCoordinate> NextOrbitGroundTrack { get; set; } = Array.Empty<GeoCoordinate>();
    public IReadOnlyList<GeoCoordinate> Footprint { get; set; } = Array.Empty<GeoCoordinate>();
    /// <summary>Angular radius of the 0°-elevation footprint on Earth (degrees).</summary>
    public double FootprintRadiusDeg { get; set; }
    /// <summary>True when the spacecraft is in full sunlight; false when in Earth's shadow.</summary>
    public bool IsSunlit { get; set; } = true;

    /// <summary>
    /// Reset all properties to safe defaults for object pool reuse.
    /// This method is called by SatelliteTrackStatePool before returning objects to the pool.
    /// </summary>
    internal void Reset()
    {
        Name = string.Empty;
        NoradId = string.Empty;
        Subpoint = new GeoCoordinate(0, 0, 0);
        LookAngles = null;
        AheadAzimuthDeg = null;
        MotionHeadingDeg = null;
        GroundTrack = Array.Empty<GeoCoordinate>();
        NextOrbitGroundTrack = Array.Empty<GeoCoordinate>();
        Footprint = Array.Empty<GeoCoordinate>();
        FootprintRadiusDeg = 0.0;
        IsSunlit = true;
    }

    /// <summary>
    /// Create a pooled SatelliteTrackState with all required properties initialized.
    /// </summary>
    public static SatelliteTrackState CreatePooled(
        string name,
        string noradId,
        GeoCoordinate subpoint,
        LookAngles? lookAngles = null,
        double? aheadAzimuthDeg = null,
        double? motionHeadingDeg = null,
        IReadOnlyList<GeoCoordinate>? groundTrack = null,
        IReadOnlyList<GeoCoordinate>? nextOrbitGroundTrack = null,
        IReadOnlyList<GeoCoordinate>? footprint = null,
        double footprintRadiusDeg = 0.0,
        bool isSunlit = true)
    {
        var state = Services.SatelliteTrackStatePool.Rent();
        
        state.Name = name;
        state.NoradId = noradId;
        state.Subpoint = subpoint;
        state.LookAngles = lookAngles;
        state.AheadAzimuthDeg = aheadAzimuthDeg;
        state.MotionHeadingDeg = motionHeadingDeg;
        state.GroundTrack = groundTrack ?? Array.Empty<GeoCoordinate>();
        state.NextOrbitGroundTrack = nextOrbitGroundTrack ?? Array.Empty<GeoCoordinate>();
        state.Footprint = footprint ?? Array.Empty<GeoCoordinate>();
        state.FootprintRadiusDeg = footprintRadiusDeg;
        state.IsSunlit = isSunlit;
        
        return state;
    }
}
