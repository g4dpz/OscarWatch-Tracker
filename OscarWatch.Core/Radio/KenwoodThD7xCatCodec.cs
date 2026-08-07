namespace OscarWatch.Core.Radio;

/// <summary>
/// Kenwood TH-D74 / TH-D75 PC-command codec for satellite tracking on Band B.
/// Commands are ASCII terminated by CR. Band B (1) is used because it is the
/// all-mode receiver that supports SSB/CW/AM as well as narrow FM.
/// </summary>
public static class KenwoodThD7xCatCodec
{
    public const char SatelliteBand = '1';
    public const int FrequencyDigits = 10;
    public const int CoarseStepHz = 5_000;
    public const int FineStepHz = 20;

    public static string BuildVfoModeCommand() => $"VM {SatelliteBand},0\r";
    public static string BuildControlBandCommand() => $"BC {SatelliteBand}\r";
    public static string BuildReadFrequencyCommand() => $"FO {SatelliteBand}\r";

    public static string BuildSetFrequencyCommand(long hz)
    {
        if (hz is <= 0 or > 9_999_999_999L)
            throw new ArgumentOutOfRangeException(nameof(hz));
        return $"FQ {SatelliteBand},{hz:D10}\r";
    }

    public static string BuildSetModeCommand(string mode) =>
        $"MD {SatelliteBand},{ResolveModeCode(mode)}\r";

    public static string BuildFineTuneCommand(bool enabled) => enabled ? "FT 1\r" : "FT 0\r";
    public static string BuildFineStepCommand() => "FS 0\r";

    /// <summary>
    /// Band-B mode codes measured on a TH-D75: NFM=6, AM=2, LSB=3, USB=4, CW=5, R-CW=9.
    /// Plain FM (0) and DV (1) are not useful/accepted on the all-mode Band B path used here.
    /// </summary>
    public static char ResolveModeCode(string mode)
    {
        var upper = (mode ?? string.Empty).Trim().ToUpperInvariant();
        return upper switch
        {
            "FM" or "FMN" or "NFM" or "DATA-FM" or "FM-DATA" or "DATA" => '6',
            "AM" => '2',
            "LSB" or "DATA-LSB" => '3',
            "USB" or "DATA-USB" => '4',
            "CW" => '5',
            "CWR" or "R-CW" => '9',
            _ => '4'
        };
    }

    public static bool UsesFineTuning(string mode)
    {
        var upper = (mode ?? string.Empty).Trim().ToUpperInvariant();
        return upper is "USB" or "DATA-USB" or "LSB" or "DATA-LSB" or "CW" or "CWR" or "R-CW" or "AM";
    }

    public static long RoundFrequencyToStep(long hz, bool fineTuning)
    {
        if (hz <= 0)
            throw new ArgumentOutOfRangeException(nameof(hz));
        var step = fineTuning ? FineStepHz : CoarseStepHz;
        return ((hz + step / 2) / step) * step;
    }

    public static bool TryParseFrequencyHz(ReadOnlySpan<char> response, out long hz)
    {
        hz = 0;
        // FO 1,0145745000,... -- frequency is ten digits immediately after the first comma.
        var comma = response.IndexOf(',');
        if (comma < 0 || comma + 1 + FrequencyDigits > response.Length)
            return false;
        if (comma < 4 || response[0] is not ('F' or 'f') || response[1] is not ('O' or 'o'))
            return false;

        long value = 0;
        for (var i = comma + 1; i < comma + 1 + FrequencyDigits; i++)
        {
            var c = response[i];
            if (c is < '0' or > '9')
                return false;
            value = value * 10 + (c - '0');
        }
        hz = value;
        return hz > 0;
    }
}
