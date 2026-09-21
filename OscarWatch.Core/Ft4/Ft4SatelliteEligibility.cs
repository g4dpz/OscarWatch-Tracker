using OscarWatch.Core.Models;

namespace OscarWatch.Core.Ft4;

/// <summary>
/// Policy for where OscarWatch FT4 may be used. FM satellites are unsuitable for FT4,
/// and FO-29’s licence does not permit this digital mode.
/// </summary>
public static class Ft4SatelliteEligibility
{
    /// <summary>NORAD catalog number for FO-29 (Fuji-OSCAR 29).</summary>
    public const string Fo29NoradId = "24278";

    public const string Fo29Name = "FO-29";

    public enum BlockReason
    {
        None = 0,
        FmMode,
        Fo29,
    }

    public static bool IsFo29(string? satelliteName, string? noradId)
    {
        if (!string.IsNullOrWhiteSpace(noradId)
            && string.Equals(noradId.Trim(), Fo29NoradId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(satelliteName))
            return false;

        var name = satelliteName.Trim();
        return name.Equals(Fo29Name, StringComparison.OrdinalIgnoreCase)
            || name.Equals("Fuji-OSCAR 29", StringComparison.OrdinalIgnoreCase)
            || name.Equals("FO29", StringComparison.OrdinalIgnoreCase);
    }

    public static BlockReason Evaluate(string? satelliteName, string? noradId, SatelliteTransponderMode? mode)
    {
        if (IsFo29(satelliteName, noradId))
            return BlockReason.Fo29;

        if (mode?.IsFmMode == true)
            return BlockReason.FmMode;

        return BlockReason.None;
    }

    public static bool IsAllowed(string? satelliteName, string? noradId, SatelliteTransponderMode? mode) =>
        Evaluate(satelliteName, noradId, mode) == BlockReason.None;

    /// <summary>Localisation key for <see cref="BlockReason"/> (empty when allowed).</summary>
    public static string StatusKey(BlockReason reason) => reason switch
    {
        BlockReason.FmMode => "Ft4.Blocked.FmSatellite",
        BlockReason.Fo29 => "Ft4.Blocked.Fo29",
        _ => ""
    };
}
