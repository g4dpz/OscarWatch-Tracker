using System.Globalization;
using System.Text;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using Serilog;

namespace OscarWatch.Diagnostics;

public sealed class DopplerPassLogger : IDopplerPassLogger
{
    private static readonly ILogger Log = Serilog.Log.ForContext<DopplerPassLogger>();

    private static readonly string[] HeaderColumns =
    [
        "Utc",
        "Event",
        "NoradId",
        "Satellite",
        "ElevDeg",
        "AzDeg",
        "RangeRateKmPerSec",
        "SlopeKmPerSec2",
        "SlewHzPerSec",
        "BaseThresholdHz",
        "EffectiveThresholdHz",
        "LeadEnabled",
        "LeadBlend",
        "LeadGainPct",
        "LeadMsRx",
        "LeadMsTx",
        "LeadRxRangeRate",
        "LeadTxRangeRate",
        "SatRxKHz",
        "SatTxKHz",
        "RadioRxKHz",
        "RadioTxKHz",
        "LastRigRxHz",
        "LastRigTxHz",
        "RxDeltaHz",
        "TxDeltaHz",
        "RxOffsetKHz",
        "TxOffsetKHz",
        "PassbandDlKHz",
        "PassbandUlKHz",
        "WroteRx",
        "WroteTx",
        "BelowThreshold",
        "Interactive",
        "CatPaused",
        "Notes"
    ];

    private readonly object _gate = new();
    private readonly string _logDirectory;
    private StreamWriter? _writer;
    private string? _activePath;

    public DopplerPassLogger(string? logDirectory = null) =>
        _logDirectory = logDirectory ?? DopplerPassLogFileNameFormat.GetDefaultLogDirectory();

    public string LogDirectory => _logDirectory;

    public string? ActiveLogPath
    {
        get
        {
            lock (_gate)
                return _activePath;
        }
    }

    public void BeginPass(RigSettings settings, RigTrackingContext context, DateTime utc)
    {
        if (!settings.DopplerPassLogEnabled)
            return;

        string path;
        lock (_gate)
        {
            CloseWriterUnlocked();

            path = DopplerPassLogFileNameFormat.ResolveUniquePath(
                LogDirectory,
                context.TrackState.Name,
                utc);
            _writer = new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
            _activePath = path;

            _writer.WriteLine(string.Join(',', HeaderColumns));
            _writer.WriteLine($"# pass_start,{Format(utc)},{Escape(context.TrackState.NoradId)},{Escape(context.TrackState.Name)}");
            _writer.WriteLine(
                $"# settings,threshold_linear={settings.DopplerThresholdLinearHz},threshold_fm={settings.DopplerThresholdFmHz},cat_delay_ms={settings.CatDelayMs},lead={settings.DopplerCatLeadEnabled},lead_ms={settings.DopplerCatLeadMs},lead_gain={settings.DopplerCatLeadGainPercent},adaptive={settings.DopplerAdaptiveThresholdEnabled}");
        }

        Log.Information("Doppler pass log started: {Path}", path);
    }

    public void Append(DopplerPassLogEntry entry)
    {
        lock (_gate)
        {
            if (_writer is null)
                return;

            _writer.WriteLine(FormatEntry(entry));
        }
    }

    public void EndPass(DateTime utc, string? reason = null)
    {
        lock (_gate)
        {
            if (_writer is null)
                return;

            _writer.WriteLine($"# pass_end,{Format(utc)},{Escape(reason ?? "")}");
            CloseWriterUnlocked();
        }
    }

    private void CloseWriterUnlocked()
    {
        _writer?.Dispose();
        _writer = null;
        _activePath = null;
    }

    internal static string FormatEntry(DopplerPassLogEntry entry) =>
        string.Join(',',
        [
            Format(entry.Utc),
            Escape(entry.Event),
            Escape(entry.NoradId),
            Escape(entry.SatelliteName),
            Format(entry.ElevationDeg),
            Format(entry.AzimuthDeg),
            Format(entry.RangeRateKmPerSec),
            Format(entry.SlopeKmPerSec2),
            Format(entry.SlewHzPerSec),
            entry.BaseThresholdHz.ToString(CultureInfo.InvariantCulture),
            entry.EffectiveThresholdHz.ToString(CultureInfo.InvariantCulture),
            entry.LeadEnabled ? "1" : "0",
            Format(entry.LeadBlend),
            entry.LeadGainPercent.ToString(CultureInfo.InvariantCulture),
            Format(entry.LeadMsRx),
            Format(entry.LeadMsTx),
            Format(entry.LeadRxRangeRate),
            Format(entry.LeadTxRangeRate),
            Format(entry.SatRxKHz),
            Format(entry.SatTxKHz),
            Format(entry.RadioRxKHz),
            Format(entry.RadioTxKHz),
            entry.LastRigRxHz.ToString(CultureInfo.InvariantCulture),
            entry.LastRigTxHz.ToString(CultureInfo.InvariantCulture),
            entry.RxDeltaHz.ToString(CultureInfo.InvariantCulture),
            entry.TxDeltaHz.ToString(CultureInfo.InvariantCulture),
            Format(entry.RxOffsetKHz),
            Format(entry.TxOffsetKHz),
            Format(entry.PassbandDlKHz),
            Format(entry.PassbandUlKHz),
            entry.WroteRx ? "1" : "0",
            entry.WroteTx ? "1" : "0",
            entry.BelowThreshold ? "1" : "0",
            entry.Interactive ? "1" : "0",
            entry.CatPaused ? "1" : "0",
            Escape(entry.Notes)
        ]);

    private static string Format(DateTime utc) =>
        utc.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string Format(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? ""
            : value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        return value;
    }
}
