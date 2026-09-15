namespace OscarWatch.Core.Qrz;

public sealed class QrzCallbookEntry
{
    public required string Call { get; init; }

    public string Name { get; init; } = "";

    public string Grid { get; init; } = "";
}
