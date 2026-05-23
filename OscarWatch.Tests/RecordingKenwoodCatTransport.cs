using OscarWatch.Rig;

namespace OscarWatch.Tests;

internal sealed class RecordingKenwoodCatTransport : IKenwoodCatTransport
{
    public List<string> SentCommands { get; } = [];
    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

    public bool SendCommand(string command, int postDelayMs = 50)
    {
        SentCommands.Add(Normalize(command));
        return true;
    }

    public string? Transact(string command, int postDelayMs = 50)
    {
        var normalized = Normalize(command);
        SentCommands.Add(normalized);
        return normalized switch
        {
            "SA;" => "SA1;",
            "FA;" => "FA00435750000;",
            "FB;" => "FB00145900000;",
            _ => normalized
        };
    }

    public void Dispose() => IsOpen = false;

    private static string Normalize(string command)
    {
        var cmd = command.Trim();
        return cmd.EndsWith(';') ? cmd : cmd + ";";
    }
}
