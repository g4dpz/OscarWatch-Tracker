namespace OscarWatch.Core.Models;

public sealed class QsoRecord
{
    public long Id { get; init; }
    public long LogbookId { get; init; }
    public DateTime QsoUtc { get; init; }
    public string Call { get; init; } = "";
    public string RstSent { get; init; } = "";
    public string RstRcvd { get; init; } = "";
    public string GridSquare { get; init; } = "";
    public string Name { get; init; } = "";
    public string Comment { get; init; } = "";
    public string SatName { get; init; } = "";
    public string Mode { get; init; } = "";
    public string ModeRx { get; init; } = "";
    public long FreqHz { get; init; }
    public long FreqRxHz { get; init; }
    public string Band { get; init; } = "";
    public string BandRx { get; init; } = "";
    public string PropMode { get; init; } = "SAT";
    public DateTime CreatedUtc { get; init; }
}

public sealed class QsoRecordCreateRequest
{
    public required long LogbookId { get; init; }
    public required DateTime QsoUtc { get; init; }
    public required string Call { get; init; }
    public string RstSent { get; init; } = "";
    public string RstRcvd { get; init; } = "";
    public string GridSquare { get; init; } = "";
    public string Name { get; init; } = "";
    public string Comment { get; init; } = "";
    public string SatName { get; init; } = "";
    public string Mode { get; init; } = "";
    public string ModeRx { get; init; } = "";
    public long FreqHz { get; init; }
    public long FreqRxHz { get; init; }
    public string Band { get; init; } = "";
    public string BandRx { get; init; } = "";
    public string PropMode { get; init; } = "SAT";
}

public sealed class QsoRecordUpdateRequest
{
    public required long Id { get; init; }
    public required DateTime QsoUtc { get; init; }
    public required string Call { get; init; }
    public string RstSent { get; init; } = "";
    public string RstRcvd { get; init; } = "";
    public string GridSquare { get; init; } = "";
    public string Name { get; init; } = "";
    public string Comment { get; init; } = "";
    public string SatName { get; init; } = "";
    public string Mode { get; init; } = "";
    public string ModeRx { get; init; } = "";
    public long FreqHz { get; init; }
    public long FreqRxHz { get; init; }
    public string Band { get; init; } = "";
    public string BandRx { get; init; } = "";
    public string PropMode { get; init; } = "SAT";
}
