using OscarWatch.Rig;

namespace OscarWatch.Tests;

internal sealed class RecordingKenwoodHtTransport : IKenwoodHtTransport
{
    public List<string> SentCommands { get; } = [];
    public bool IsOpen { get; private set; }
    public string? FrequencyResponse { get; set; } = "FO 1,0145745000,0,0,0,0";
    public bool FailSets { get; set; }

    public void Open() => IsOpen = true;

    public bool SendCommand(string command, int postDelayMs = 50)
    {
        SentCommands.Add(Normalize(command));
        return !FailSets;
    }

    public string? Transact(string command, int postDelayMs = 50)
    {
        var normalized = Normalize(command);
        SentCommands.Add(normalized);
        if (normalized.StartsWith("FO ", StringComparison.OrdinalIgnoreCase))
            return FrequencyResponse;
        return FailSets ? null : normalized.TrimEnd('\r');
    }

    public void Dispose() => IsOpen = false;

    private static string Normalize(string command) => command.TrimEnd('\r', '\n') + "\r";
}
