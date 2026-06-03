using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

internal sealed class RecordingKenwoodCatTransport : IKenwoodCatTransport
{
    public long FaHz { get; set; } = 435_750_000;
    public long FbHz { get; set; } = 145_900_000;
    public bool SatelliteStatusOn { get; set; } = true;
    public List<string> SentCommands { get; } = [];
    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

    public bool SendCommand(string command, int postDelayMs = 50)
    {
        var normalized = Normalize(command);
        SentCommands.Add(normalized);
        ApplySatelliteModeCommand(normalized);
        ApplySetFrequency(normalized);
        return true;
    }

    public string? Transact(string command, int postDelayMs = 50)
    {
        var normalized = Normalize(command);
        SentCommands.Add(normalized);
        ApplySatelliteModeCommand(normalized);
        return normalized switch
        {
            "SA1010110;" => SatelliteStatusOn ? "SA1;" : "SA0;",
            "SA10100000;" => SatelliteStatusOn ? "SA1;" : "SA0;",
            "SA0010000;" => "SA0;",
            "SA0;" => "SA0;",
            "RX;" => "RX0;",
            "SA;" => SatelliteStatusOn ? "SA1;" : "SA0;",
            "FA;" => KenwoodCatCodec.BuildSetFrequencyCommand('A', FaHz),
            "FB;" => KenwoodCatCodec.BuildSetFrequencyCommand('B', FbHz),
            _ => normalized
        };
    }

    public void Dispose() => IsOpen = false;

    private void ApplySatelliteModeCommand(string normalized)
    {
        if (normalized is "SA0010000;" or "SA0;")
            SatelliteStatusOn = false;
    }

    private void ApplySetFrequency(string normalized)
    {
        if (KenwoodCatCodec.TryParseFrequencyHz(normalized, out var hz) && hz > 0)
        {
            if (normalized.StartsWith("FA", StringComparison.OrdinalIgnoreCase))
                FaHz = hz;
            else if (normalized.StartsWith("FB", StringComparison.OrdinalIgnoreCase))
                FbHz = hz;
        }
    }

    private static string Normalize(string command)
    {
        var cmd = command.Trim();
        return cmd.EndsWith(';') ? cmd : cmd + ";";
    }
}
