namespace OscarWatch.Core.Geo;

public static class MaidenheadGrid
{
    public static string FromLatLon(double latitudeDeg, double longitudeDeg, int length = 6)
    {
        if (length is not (4 or 6 or 8))
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be 4, 6, or 8.");

        var lat = latitudeDeg + 90.0;
        var lon = longitudeDeg + 180.0;

        var field1 = (char)('A' + (int)(lon / 20));
        var field2 = (char)('A' + (int)(lat / 10));
        var square1 = (char)('0' + (int)((lon % 20) / 2));
        var square2 = (char)('0' + (int)((lat % 10) / 1));

        var result = $"{field1}{field2}{square1}{square2}";
        if (length <= 4)
            return result;

        var subsquare1 = (char)('a' + (int)((lon % 2) / (2.0 / 24)));
        var subsquare2 = (char)('a' + (int)((lat % 1) / (1.0 / 24)));
        result += $"{subsquare1}{subsquare2}";

        if (length <= 6)
            return result;

        var extra1 = (char)('0' + (int)(((lon % (2.0 / 24)) / (2.0 / 24 / 10))));
        var extra2 = (char)('0' + (int)(((lat % (1.0 / 24)) / (1.0 / 24 / 10))));
        return result + $"{extra1}{extra2}";
    }

    public static (double LatitudeDeg, double LongitudeDeg) ToLatLonCenter(string gridSquare)
    {
        if (string.IsNullOrWhiteSpace(gridSquare) || gridSquare.Length < 4)
            throw new ArgumentException("Grid square must be at least 4 characters.", nameof(gridSquare));

        var g = gridSquare.Trim().ToUpperInvariant();
        var lon = (g[0] - 'A') * 20.0 - 180.0;
        var lat = (g[1] - 'A') * 10.0 - 90.0;
        lon += (g[2] - '0') * 2.0 + 1.0;
        lat += (g[3] - '0') * 1.0 + 0.5;

        if (g.Length >= 6)
        {
            lon += ((g[4] | 0x20) - 'a') * (2.0 / 24) + (1.0 / 24);
            lat += ((g[5] | 0x20) - 'a') * (1.0 / 24) + (0.5 / 24);
        }

        if (g.Length >= 8)
        {
            lon += (g[6] - '0') * (2.0 / 240) + (1.0 / 240);
            lat += (g[7] - '0') * (1.0 / 240) + (0.5 / 240);
        }

        return (lat, lon);
    }
}
