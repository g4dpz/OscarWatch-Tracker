namespace OscarWatch.Core.Radio;

/// <summary>
/// Kenwood TS-2000 ASCII CAT command encoding (Hamlib-compatible subset).
/// Frequencies are 11-digit Hz; CTCSS indices are 1-based per ts2000_ctcss_list.
/// </summary>
public static class KenwoodCatCodec
{
    public const int FrequencyDigits = 11;
    public const int ToneTableBase = 1;

    /// <summary>TS-2000 CTCSS list (Hz), Hamlib ts2000_ctcss_list — excludes 17500 tone.</summary>
    public static readonly int[] CtcssTonesHz =
    [
        670, 719, 744, 770, 797, 825, 854, 885, 915, 948,
        974, 1000, 1035, 1072, 1109, 1148, 1188, 1230, 1273, 1318,
        1365, 1413, 1462, 1514, 1567, 1622, 1679, 1738, 1799, 1862,
        1928, 2035, 2107, 2181, 2257, 2336, 2418, 2503
    ];

    public static string BuildSetFrequencyCommand(char vfoLetter, long hz)
    {
        var letter = char.ToUpperInvariant(vfoLetter);
        if (letter is not ('A' or 'B' or 'C'))
            throw new ArgumentOutOfRangeException(nameof(vfoLetter));

        if (hz < 0)
            throw new ArgumentOutOfRangeException(nameof(hz));

        return $"F{letter}{hz:D11};";
    }

    public static string BuildReadFrequencyCommand(char vfoLetter) =>
        $"F{char.ToUpperInvariant(vfoLetter)};";

    public static string BuildSetModeCommand(char modeCode) => $"MD{modeCode};";

    public static string BuildSatelliteStatusQuery() => "SA;";

    /// <summary>
    /// Enter SATL (SatPC32-compatible): P1 on, mem 0, Main=downlink/Sub=uplink, CTRL main, TRACE/TRACE REV on, VFO mode.
    /// </summary>
    public static string BuildSetSatelliteModeOnCommand() => "SA1010110;";

    /// <summary>Encode-tone off commands SatPC32 sends when enabling CAT (pre-tracking).</summary>
    public static readonly string[] SatelliteModeEntryToneOffSequence = ["TO0;", "TO0;"];

    /// <summary>SatPC32: extended Auto Information on (reduces need for explicit reads during tracking).</summary>
    public static string BuildAutoinfoExtendedCommand() => "AI2;";

    /// <summary>SatPC32 SAT entry: sent after initial <c>FA;</c> read (TS-2000 satellite CAT handshake).</summary>
    public static string BuildSatelliteEntryTsCommand() => "TS1;";

    /// <summary>SatPC32: RF power level 50 (PC P1=050).</summary>
    public static string BuildSatellitePowerLevelCommand() => "PC050;";

    /// <summary>FA; polls SatPC32 sends to hold the CAT link after frequency updates.</summary>
    public const int SatelliteLinkHoldPollCount = 7;

    /// <summary>Wait for FA;/FB; reply (SatPC32 holds link until response).</summary>
    public const int FrequencyReadTimeoutMs = 450;

    public static int GetReplyTimeoutMs(string command, int postDelayMs)
    {
        var body = command.Trim().TrimEnd(';');
        if (body.Length == 2 && body[0] is 'F' or 'f')
            return FrequencyReadTimeoutMs;

        return body.ToUpperInvariant() switch
        {
            "SA" or "RX" or "ID" => Math.Max(postDelayMs + 400, 600),
            _ => Math.Max(postDelayMs + 200, 400)
        };
    }

    public static bool IsReadCommand(string command)
    {
        var body = command.Trim().TrimEnd(';');
        if (body.Length == 2 && body[0] is 'F' or 'f')
            return body[1] is 'A' or 'a' or 'B' or 'b' or 'C' or 'c';

        return body.Equals("SA", StringComparison.OrdinalIgnoreCase)
            || body.Equals("RX", StringComparison.OrdinalIgnoreCase)
            || body.Equals("ID", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>SatPC32 SAT on with CTRL on sub (before uplink <c>MD</c> / tone on Sub).</summary>
    public static string BuildSetSatelliteModeOnSubControlCommand() => "SA1011110;";

    /// <summary>SatPC32 band select after <c>FA</c>/<c>FB</c> in SATL (main then sub band index).</summary>
    public static string BuildSatelliteBandSelectMainCommand() => "SM10000;";

    public static string BuildSatelliteBandSelectSubCommand(long hz) =>
        hz >= 200_000_000 ? "SM00004;" : "SM00021;";

    /// <summary>Short SAT off (manual P1=0 only).</summary>
    public static string BuildSetSatelliteModeOffCommand() => "SA0;";

    /// <summary>
    /// SatPC32 exit: read RX status, clear encode tone, reset TN table entries, then SAT off with layout preserved.
    /// </summary>
    public static readonly string[] SatelliteModeExitSequence =
    [
        "RX;",
        "TN39;",
        "TO0;",
        "TN39;",
        "SA0010000;"
    ];

    public static bool IsSatelliteModeExitReadCommand(string command) =>
        string.Equals(command, "RX;", StringComparison.OrdinalIgnoreCase);

    public static string BuildAutoinfoOffCommand() => "AI0;";

    public static string BuildSelectVfoCommand(bool vfoB) => vfoB ? "FR1;" : "FR0;";

    /// <summary>CTCSS squelch (TSQL) tone frequency — Hamlib set_ctcss_sql.</summary>
    public static string BuildCtcssFrequencyCommand(int oneBasedIndex) =>
        $"CN{oneBasedIndex:D2};";

    /// <summary>CTCSS encode tone frequency — Hamlib set_ctcss_tone (TN).</summary>
    public static string BuildToneFrequencyCommand(int oneBasedIndex) =>
        $"TN{oneBasedIndex:D2};";

    /// <summary>CTCSS squelch (TSQL) on/off — Hamlib RIG_FUNC_TSQL.</summary>
    public static string BuildCtcssEnableCommand(bool on) => on ? "CT1;" : "CT0;";

    /// <summary>CTCSS encode on/off — Hamlib RIG_FUNC_TONE.</summary>
    public static string BuildToneEnableCommand(bool on) => on ? "TO1;" : "TO0;";

    /// <summary>DC P1=0 P2=0 — TX and CTRL on main (VFO A / downlink in SATL).</summary>
    public static string BuildControlMainCommand() => "DC00;";

    /// <summary>DC P1=0 P2=1 — TX main, CTRL sub (tone/CTCSS and <c>MD</c> on sub band).</summary>
    public static string BuildControlSubCommand() => "DC01;";

    public static bool TryParseFrequencyHz(ReadOnlySpan<char> response, out long hz)
    {
        hz = 0;
        if (response.Length < 2 + FrequencyDigits)
            return false;

        var start = response[0] is 'F' or 'f' ? 2 : 0;
        if (start + FrequencyDigits > response.Length)
            return false;

        long value = 0;
        for (var i = 0; i < FrequencyDigits; i++)
        {
            var c = response[start + i];
            if (c is < '0' or > '9')
                return false;
            value = value * 10 + (c - '0');
        }

        hz = value;
        return true;
    }

    public static bool TryParseSatelliteOn(ReadOnlySpan<char> response)
    {
        // Hamlib: SA response, satellite on when retbuf[2] == '1'
        for (var i = 0; i < response.Length - 2; i++)
        {
            if ((response[i] is 'S' or 's') && (response[i + 1] is 'A' or 'a'))
                return response[i + 2] == '1';
        }

        return false;
    }

    public static bool TryGetModeCode(string mode, out char modeCode)
    {
        modeCode = default;
        var upper = mode.ToUpperInvariant();
        var code = upper switch
        {
            "LSB" or "DATA-LSB" => '1',
            "USB" or "DATA-USB" => '2',
            "CW" => '3',
            "FM" or "FMN" => '4',
            "AM" => '5',
            _ => (char)0
        };

        if (code == 0)
            return false;

        modeCode = code;
        return true;
    }

    public static bool TryGetCtcssIndex(double toneHz, out int oneBasedIndex)
    {
        oneBasedIndex = 0;
        // Hamlib tone_t values are tenths of a Hz (670 => 67.0 Hz).
        var target = (int)Math.Round(toneHz * 10.0);
        var best = -1;
        var bestDiff = int.MaxValue;

        for (var i = 0; i < CtcssTonesHz.Length; i++)
        {
            var diff = Math.Abs(CtcssTonesHz[i] - target);
            if (diff >= bestDiff)
                continue;

            bestDiff = diff;
            best = i;
        }

        if (best < 0 || bestDiff > 5)
            return false;

        oneBasedIndex = best + ToneTableBase;
        return true;
    }

}
