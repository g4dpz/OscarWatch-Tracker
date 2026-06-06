namespace OscarWatch.Core.Models;

public sealed class CloudlogLogbookInfo
{
    public int LogbookId { get; init; }

    public string LogbookName { get; init; } = "";

    public string PublicSlug { get; init; } = "";

    public string? AccessLevel { get; init; }
}

public sealed class CloudlogLogbooksResult
{
    public bool Ok { get; init; }
    public IReadOnlyList<CloudlogLogbookInfo> Logbooks { get; init; } = [];
    public string? ErrorMessage { get; init; }

    public static CloudlogLogbooksResult Success(IReadOnlyList<CloudlogLogbookInfo> logbooks) =>
        new() { Ok = true, Logbooks = logbooks };

    public static CloudlogLogbooksResult Failed(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

public sealed class CloudlogGridCheckResult
{
    public string Grid { get; init; } = "";

    public bool IsWorked { get; init; }
}
