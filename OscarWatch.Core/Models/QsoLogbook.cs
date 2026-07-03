namespace OscarWatch.Core.Models;

public sealed class QsoLogbook
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public DateTime CreatedUtc { get; init; }
    public DateTime? StartedUtc { get; init; }
    public DateTime? EndedUtc { get; init; }
    public string MyCallsign { get; init; } = "";
    public string MyGridSquare { get; init; } = "";
    public string Notes { get; init; } = "";
}

public sealed class QsoLogbookCreateRequest
{
    public required string Name { get; init; }
    public string MyCallsign { get; init; } = "";
    public string MyGridSquare { get; init; } = "";
    public DateTime? StartedUtc { get; init; }
    public DateTime? EndedUtc { get; init; }
    public string Notes { get; init; } = "";
}

public sealed class QsoLogbookUpdateRequest
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public string MyCallsign { get; init; } = "";
    public string MyGridSquare { get; init; } = "";
}

/// <summary>Result from the new/edit logbook dialogue.</summary>
public sealed class LogbookDetailsDialogResult
{
    public long? UpdateLogbookId { get; init; }
    public required string Name { get; init; }
    public string MyCallsign { get; init; } = "";
    public string MyGridSquare { get; init; } = "";
}
