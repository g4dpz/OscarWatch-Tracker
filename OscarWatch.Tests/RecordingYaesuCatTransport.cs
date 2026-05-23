using OscarWatch.Rig;

namespace OscarWatch.Tests;

internal sealed class RecordingYaesuCatTransport : IYaesuCatTransport
{
    public List<byte[]> SentFrames { get; } = [];
    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

    public bool SendFrame(ReadOnlySpan<byte> frame, int postDelayMs = 50)
    {
        SentFrames.Add(frame.ToArray());
        return true;
    }

    public byte[]? QueryFrame(ReadOnlySpan<byte> pollFrame, int postDelayMs = 50)
    {
        SentFrames.Add(pollFrame.ToArray());
        return null;
    }

    public void Dispose() => IsOpen = false;
}
