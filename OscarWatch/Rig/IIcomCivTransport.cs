namespace OscarWatch.Rig;

internal interface IIcomCivTransport : IDisposable
{
    bool IsOpen { get; }

    void Open();

    byte[] WriteCommand(ReadOnlySpan<byte> body, int postDelayMs = 50);
}
