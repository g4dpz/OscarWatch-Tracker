using OscarWatch.Core.Services;
using Serilog;

namespace OscarWatch.Diagnostics;

public sealed class SerilogTrackingDiagnostics : ITrackingDiagnostics
{
    private static readonly ILogger Log = Serilog.Log.ForContext<SerilogTrackingDiagnostics>();

    public void LookAnglesSkipped(string noradId, DateTime utc, Exception exception) =>
        Log.Debug(exception, "Look-angle computation skipped for NORAD {NoradId} at {Utc:O}", noradId, utc);

    public void SatelliteStateSkipped(string noradId, DateTime utc, Exception exception) =>
        Log.Debug(exception, "Satellite state skipped for NORAD {NoradId} at {Utc:O}", noradId, utc);
}
