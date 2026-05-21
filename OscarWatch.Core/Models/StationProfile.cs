namespace OscarWatch.Core.Models;

public sealed class StationProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = "Home";
    public double LatitudeDeg { get; set; } = 51.5;
    public double LongitudeDeg { get; set; } = -0.1;
    public double AltitudeMetersAsl { get; set; } = 50;
    public string GridSquare { get; set; } = "IO91wm";

    public GroundStation ToGroundStation() => new()
    {
        DisplayName = DisplayName,
        LatitudeDeg = LatitudeDeg,
        LongitudeDeg = LongitudeDeg,
        AltitudeMetersAsl = AltitudeMetersAsl,
        GridSquare = GridSquare
    };

    public static StationProfile FromGroundStation(GroundStation gs, string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString("N"),
        DisplayName = gs.DisplayName,
        LatitudeDeg = gs.LatitudeDeg,
        LongitudeDeg = gs.LongitudeDeg,
        AltitudeMetersAsl = gs.AltitudeMetersAsl,
        GridSquare = gs.GridSquare
    };
}
