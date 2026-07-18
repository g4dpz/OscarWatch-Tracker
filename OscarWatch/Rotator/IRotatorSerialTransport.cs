namespace OscarWatch.Rotator;

/// <summary>Byte-stream transport for serial-protocol rotator drivers (COM or raw TCP).</summary>
internal interface IRotatorSerialTransport : IDisposable
{
    bool IsOpen { get; }

    void Open();

    void Write(string text);

    void Write(byte[] buffer, int offset, int count);

    void DiscardInBuffer();

    void DiscardOutBuffer();

    string ReadLine();

    string ReadExisting();

    int Read(byte[] buffer, int offset, int count);

    /// <summary>No-op on TCP; used by SAEBRTrack on serial adapters.</summary>
    bool DtrEnable { get; set; }

    /// <summary>No-op on TCP; used by SAEBRTrack on serial adapters.</summary>
    bool RtsEnable { get; set; }
}
