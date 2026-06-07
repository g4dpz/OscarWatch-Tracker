namespace OscarWatch.Core.Services;

/// <summary>Optional tracking diagnostics sink (e.g. Serilog in the desktop app).</summary>
public interface ITrackingDiagnostics
{
    void LookAnglesSkipped(string noradId, DateTime utc, Exception exception);

    void SatelliteStateSkipped(string noradId, DateTime utc, Exception exception);
}

public sealed class NullTrackingDiagnostics : ITrackingDiagnostics
{
    public static NullTrackingDiagnostics Instance { get; } = new();

    public void LookAnglesSkipped(string noradId, DateTime utc, Exception exception) { }

    public void SatelliteStateSkipped(string noradId, DateTime utc, Exception exception) { }
}
