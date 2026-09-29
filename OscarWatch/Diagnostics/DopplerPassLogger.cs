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

    // Reusable StringBuilder buffer for CSV entry formatting to avoid string array and Join allocations
    private static readonly StringBuilder _entryBuffer = new(1024);

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
        "DialTracking",
        "MainDialHz",
        "DialVsCatHz",
        "VfoStable",
        "RigTracking",
        "CatPaused",
        "SkipReason",
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

            try
            {
                var pruned = DopplerPassLogFileNameFormat.PruneOlderThanRetention(LogDirectory);
                if (pruned > 0)
                    Log.Information("Pruned {Count} Doppler pass log(s) older than {Days} days", pruned, DopplerPassLogFileNameFormat.RetainedFileDays);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to prune old Doppler pass logs in {Directory}", LogDirectory);
            }

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

    internal static string FormatEntry(DopplerPassLogEntry entry)
    {
        lock (_entryBuffer)
        {
            _entryBuffer.Clear();
            
            // Build CSV line using StringBuilder buffer with direct value appending for maximum efficiency
            AppendDateTimeField(_entryBuffer, entry.Utc);
            AppendEscapedField(_entryBuffer, entry.Event);
            AppendEscapedField(_entryBuffer, entry.NoradId);
            AppendEscapedField(_entryBuffer, entry.SatelliteName);
            AppendDoubleField(_entryBuffer, entry.ElevationDeg);
            AppendDoubleField(_entryBuffer, entry.AzimuthDeg);
            AppendDoubleField(_entryBuffer, entry.RangeRateKmPerSec);
            AppendDoubleField(_entryBuffer, entry.SlopeKmPerSec2);
            AppendDoubleField(_entryBuffer, entry.SlewHzPerSec);
            AppendIntField(_entryBuffer, entry.BaseThresholdHz);
            AppendIntField(_entryBuffer, entry.EffectiveThresholdHz);
            AppendBoolField(_entryBuffer, entry.LeadEnabled);
            AppendDoubleField(_entryBuffer, entry.LeadBlend);
            AppendIntField(_entryBuffer, entry.LeadGainPercent);
            AppendDoubleField(_entryBuffer, entry.LeadMsRx);
            AppendDoubleField(_entryBuffer, entry.LeadMsTx);
            AppendDoubleField(_entryBuffer, entry.LeadRxRangeRate);
            AppendDoubleField(_entryBuffer, entry.LeadTxRangeRate);
            AppendDoubleField(_entryBuffer, entry.SatRxKHz);
            AppendDoubleField(_entryBuffer, entry.SatTxKHz);
            AppendDoubleField(_entryBuffer, entry.RadioRxKHz);
            AppendDoubleField(_entryBuffer, entry.RadioTxKHz);
            AppendLongField(_entryBuffer, entry.LastRigRxHz);
            AppendLongField(_entryBuffer, entry.LastRigTxHz);
            AppendLongField(_entryBuffer, entry.RxDeltaHz);
            AppendLongField(_entryBuffer, entry.TxDeltaHz);
            AppendDoubleField(_entryBuffer, entry.RxOffsetKHz);
            AppendDoubleField(_entryBuffer, entry.TxOffsetKHz);
            AppendDoubleField(_entryBuffer, entry.PassbandDlKHz);
            AppendDoubleField(_entryBuffer, entry.PassbandUlKHz);
            AppendBoolField(_entryBuffer, entry.WroteRx);
            AppendBoolField(_entryBuffer, entry.WroteTx);
            AppendBoolField(_entryBuffer, entry.BelowThreshold);
            AppendBoolField(_entryBuffer, entry.Interactive);
            AppendEscapedField(_entryBuffer, entry.DialTracking);
            AppendLongField(_entryBuffer, entry.MainDialHz);
            AppendLongField(_entryBuffer, entry.DialVsCatHz);
            AppendBoolField(_entryBuffer, entry.VfoStable);
            AppendBoolField(_entryBuffer, entry.RigTracking);
            AppendBoolField(_entryBuffer, entry.CatPaused);
            AppendEscapedField(_entryBuffer, entry.SkipReason);
            AppendEscapedField(_entryBuffer, entry.Notes);
            
            return _entryBuffer.ToString();
        }
    }

    private static string Format(DateTime utc) =>
        utc.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string Format(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? ""
            : value.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>
    /// Appends a CSV field to the StringBuilder buffer with comma separator.
    /// Used by FormatEntry optimization to eliminate string array allocation.
    /// </summary>
    private static void AppendField(StringBuilder buffer, string value)
    {
        if (buffer.Length > 0)
            buffer.Append(',');
        buffer.Append(value);
    }

    /// <summary>
    /// Appends a DateTime field directly to StringBuilder to eliminate string allocation.
    /// </summary>
    private static void AppendDateTimeField(StringBuilder buffer, DateTime utc)
    {
        if (buffer.Length > 0)
            buffer.Append(',');
        
        // Manually format to avoid string allocation: yyyy-MM-dd HH:mm:ss.fff
        buffer.Append(utc.Year.ToString("D4", CultureInfo.InvariantCulture));
        buffer.Append('-');
        buffer.Append(utc.Month.ToString("D2", CultureInfo.InvariantCulture));
        buffer.Append('-');
        buffer.Append(utc.Day.ToString("D2", CultureInfo.InvariantCulture));
        buffer.Append(' ');
        buffer.Append(utc.Hour.ToString("D2", CultureInfo.InvariantCulture));
        buffer.Append(':');
        buffer.Append(utc.Minute.ToString("D2", CultureInfo.InvariantCulture));
        buffer.Append(':');
        buffer.Append(utc.Second.ToString("D2", CultureInfo.InvariantCulture));
        buffer.Append('.');
        buffer.Append(utc.Millisecond.ToString("D3", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Appends a double field directly to StringBuilder to eliminate string allocation.
    /// </summary>
    private static void AppendDoubleField(StringBuilder buffer, double value)
    {
        if (buffer.Length > 0)
            buffer.Append(',');

        if (double.IsNaN(value) || double.IsInfinity(value))
            return; // Empty field for invalid values

        // Use StringBuilder's AppendFormat for efficient numeric formatting
        buffer.AppendFormat(CultureInfo.InvariantCulture, "{0:0.######}", value);
    }

    /// <summary>
    /// Appends an integer field directly to StringBuilder to eliminate string allocation.
    /// </summary>
    private static void AppendIntField(StringBuilder buffer, int value)
    {
        if (buffer.Length > 0)
            buffer.Append(',');
        
        buffer.AppendFormat(CultureInfo.InvariantCulture, "{0}", value);
    }

    /// <summary>
    /// Appends a long field directly to StringBuilder to eliminate string allocation.
    /// </summary>
    private static void AppendLongField(StringBuilder buffer, long value)
    {
        if (buffer.Length > 0)
            buffer.Append(',');
        
        buffer.AppendFormat(CultureInfo.InvariantCulture, "{0}", value);
    }

    /// <summary>
    /// Appends a boolean field as "1" or "0" directly to StringBuilder.
    /// </summary>
    private static void AppendBoolField(StringBuilder buffer, bool value)
    {
        if (buffer.Length > 0)
            buffer.Append(',');
        
        buffer.Append(value ? '1' : '0');
    }

    /// <summary>
    /// Appends an escaped string field directly to StringBuilder to eliminate temporary string allocation.
    /// </summary>
    private static void AppendEscapedField(StringBuilder buffer, string? value)
    {
        if (buffer.Length > 0)
            buffer.Append(',');

        if (string.IsNullOrEmpty(value))
            return; // Empty field

        // Check if escaping is needed
        bool needsEscaping = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        
        if (needsEscaping)
        {
            buffer.Append('"');
            // Escape quotes by doubling them
            foreach (char c in value)
            {
                if (c == '"')
                    buffer.Append("\"\"");
                else
                    buffer.Append(c);
            }
            buffer.Append('"');
        }
        else
        {
            buffer.Append(value);
        }
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        return value;
    }
}
