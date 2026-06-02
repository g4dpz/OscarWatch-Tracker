namespace OscarWatch.Rig;

internal interface IYaesuNewCatTransport : IDisposable
{
    bool IsOpen { get; }
    void Open();
    bool SendCommand(string command, int postDelayMs = 50);
    string? Transact(string command, int postDelayMs = 50);
}
