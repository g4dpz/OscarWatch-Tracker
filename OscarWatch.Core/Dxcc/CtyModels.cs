namespace OscarWatch.Core.Dxcc;

public sealed class CtyEntity
{
    public required string Name { get; init; }
    public required string PrimaryPrefix { get; init; }
    public bool IsWaeOnly { get; init; }
    public int CQZone { get; init; }
    public int ITUZone { get; init; }
    public string Continent { get; init; } = "";
}

public sealed class CtyPrefixEntry
{
    public required string Prefix { get; init; }
    public required CtyEntity Entity { get; init; }
    public bool IsExactCall { get; init; }
}

public sealed class CtyDatDatabase
{
    public IReadOnlyList<CtyPrefixEntry> PrefixesByLengthDescending { get; init; } = [];
    public IReadOnlyDictionary<string, CtyEntity> ExactCalls { get; init; } =
        new Dictionary<string, CtyEntity>(StringComparer.Ordinal);
}

public readonly record struct CtyMatch(CtyEntity Entity, string MatchedPrefix, bool IsExactCall);
