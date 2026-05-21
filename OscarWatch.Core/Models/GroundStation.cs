namespace OscarWatch.Core.Models;

public sealed class GroundStation
{
    public string DisplayName { get; set; } = "Home";
    public double LatitudeDeg { get; set; } = 51.5;
    public double LongitudeDeg { get; set; } = -0.1;
    public double AltitudeMetersAsl { get; set; } = 50;
    public string GridSquare { get; set; } = "IO91wm";

    public double AltitudeKm => AltitudeMetersAsl / 1000.0;
}
