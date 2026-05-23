namespace OscarWatch.Core.Models;

public sealed class IlluminationSegment
{
    public required DateTime StartUtc { get; init; }
    public required DateTime EndUtc { get; init; }
    public required bool IsSunlit { get; init; }

    public TimeSpan Duration => EndUtc - StartUtc;
}
