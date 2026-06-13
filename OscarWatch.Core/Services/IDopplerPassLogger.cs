using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IDopplerPassLogger
{
    void BeginPass(RigSettings settings, RigTrackingContext context, DateTime utc);

    void Append(DopplerPassLogEntry entry);

    void EndPass(DateTime utc, string? reason = null);

    string LogDirectory { get; }

    string? ActiveLogPath { get; }
}

public sealed class NullDopplerPassLogger : IDopplerPassLogger
{
    public static NullDopplerPassLogger Instance { get; } = new();

    public string LogDirectory => "";

    public string? ActiveLogPath => null;

    public void BeginPass(RigSettings settings, RigTrackingContext context, DateTime utc) { }

    public void Append(DopplerPassLogEntry entry) { }

    public void EndPass(DateTime utc, string? reason = null) { }
}
