namespace OscarWatch.Core.Models;

public sealed class HamsAtUpcomingAlert
{
    public string Id { get; init; } = "";

    public string Callsign { get; init; } = "";

    public string Comment { get; init; } = "";

    public string Url { get; init; } = "";

    public string Mode { get; init; } = "";

    public DateTime AosUtc { get; init; }

    public DateTime LosUtc { get; init; }

    public IReadOnlyList<string> Grids { get; init; } = [];

    public double? Mhz { get; init; }

    public bool IsWorkable { get; init; }

    public HamsAtSatelliteInfo? Satellite { get; init; }
}

public sealed class HamsAtSatelliteInfo
{
    public string Name { get; init; } = "";

    public int Number { get; init; }
}
