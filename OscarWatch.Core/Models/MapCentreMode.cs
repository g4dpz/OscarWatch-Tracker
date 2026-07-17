namespace OscarWatch.Core.Models;

/// <summary>How the world map chooses its centre longitude.</summary>
public enum MapCentreMode
{
    /// <summary>Greenwich (lon 0°) at mid-map — historical default.</summary>
    Greenwich = 0,

    /// <summary>Centre on the ground station longitude (gridsquare / lat-lon).</summary>
    Station = 1,

    /// <summary>Centre on a user-specified longitude.</summary>
    Custom = 2,
}
