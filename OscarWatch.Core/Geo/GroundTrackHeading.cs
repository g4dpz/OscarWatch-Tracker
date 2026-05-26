using OscarWatch.Core.Models;

namespace OscarWatch.Core.Geo;

public static class GroundTrackHeading
{
    /// <summary>
    /// Estimates ground-track heading at the subpoint from the nearest forward track segment.
    /// Returns degrees clockwise from north, or null when the track is too short.
    /// </summary>
    public static double? EstimateHeadingDeg(
        GeoCoordinate subpoint,
        IReadOnlyList<GeoCoordinate> groundTrack)
    {
        if (groundTrack.Count < 2)
            return null;

        var bestSegment = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < groundTrack.Count - 1; i++)
        {
            var start = groundTrack[i];
            var end = groundTrack[i + 1];
            var midLat = (start.LatitudeDeg + end.LatitudeDeg) / 2.0;
            var midLon = EquirectangularProjection.NormalizeLongitudeNear(
                (start.LongitudeDeg + end.LongitudeDeg) / 2.0,
                subpoint.LongitudeDeg);
            var distance = SphericalGeo.AngularDistanceDeg(
                subpoint.LatitudeDeg,
                subpoint.LongitudeDeg,
                midLat,
                midLon);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSegment = i;
            }
        }

        var from = groundTrack[bestSegment];
        var to = groundTrack[bestSegment + 1];
        return SphericalGeo.InitialBearingDeg(
            from.LatitudeDeg,
            from.LongitudeDeg,
            to.LatitudeDeg,
            to.LongitudeDeg);
    }
}
