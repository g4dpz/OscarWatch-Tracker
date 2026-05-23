namespace OscarWatch.Rig;

internal interface IYaesuCatTransport : IDisposable
{
    bool IsOpen { get; }
    void Open();
    bool SendFrame(ReadOnlySpan<byte> frame, int postDelayMs = 50);
    byte[]? QueryFrame(ReadOnlySpan<byte> pollFrame, int postDelayMs = 50);
}
