namespace OscarWatch.Tests;

internal sealed class RecordingYaesuNewCatTransport : OscarWatch.Rig.IYaesuNewCatTransport
{
    public List<string> SentCommands { get; } = [];
    public Queue<string> Responses { get; } = new();
    public long VfoAHz { get; set; } = 435_750_000;
    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

    public bool SendCommand(string command, int postDelayMs = 50) =>
        Transact(command, postDelayMs) is not null;

    public string? Transact(string command, int postDelayMs = 50)
    {
        if (!IsOpen)
            return null;

        var cmd = command.TrimEnd();
        if (!cmd.EndsWith(';'))
            cmd += ';';

        SentCommands.Add(cmd);

        if (Responses.Count > 0)
            return Responses.Dequeue();

        if (cmd is "FA;" or "FB;")
            return cmd[1] == 'B'
                ? $"FB{VfoAHz:D9};"
                : $"FA{VfoAHz:D9};";

        return cmd;
    }

    public void Dispose() => IsOpen = false;
}
