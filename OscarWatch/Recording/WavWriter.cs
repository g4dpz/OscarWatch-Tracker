namespace OscarWatch.Recording;

public sealed class WavWriter : IDisposable
{
    private const int HeaderSize = 44;
    private readonly FileStream _stream;
    private readonly BinaryWriter _writer;
    private readonly int _sampleRate;
    private readonly short _channels;
    private long _dataBytes;
    private bool _finalized;

    public WavWriter(string path, int sampleRate, short channels)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        _writer = new BinaryWriter(_stream);
        WriteHeader(0);
    }

    public void WritePcm16(ReadOnlySpan<byte> pcm16Le)
    {
        ObjectDisposedException.ThrowIf(_finalized, this);
        if (pcm16Le.IsEmpty)
            return;

        _writer.Write(pcm16Le);
        _dataBytes += pcm16Le.Length;
    }

    public void FinalizeHeader()
    {
        if (_finalized)
            return;

        _writer.Flush();
        _stream.Seek(0, SeekOrigin.Begin);
        WriteHeader(_dataBytes);
        _finalized = true;
    }

    private void WriteHeader(long dataBytes)
    {
        var byteRate = _sampleRate * _channels * 2;
        var blockAlign = (short)(_channels * 2);

        _writer.Write("RIFF"u8);
        _writer.Write((int)(36 + dataBytes));
        _writer.Write("WAVE"u8);
        _writer.Write("fmt "u8);
        _writer.Write(16);
        _writer.Write((short)1);
        _writer.Write(_channels);
        _writer.Write(_sampleRate);
        _writer.Write(byteRate);
        _writer.Write(blockAlign);
        _writer.Write((short)16);
        _writer.Write("data"u8);
        _writer.Write((int)dataBytes);
    }

    public void Dispose()
    {
        if (!_finalized)
            FinalizeHeader();

        _writer.Dispose();
        _stream.Dispose();
    }
}
