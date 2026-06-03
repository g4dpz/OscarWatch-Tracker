namespace OscarWatch.Rig;

internal interface IKenwoodCatTransport : IDisposable
{
    bool IsOpen { get; }
    void Open();

    /// <summary>Sets and similar commands that often return no CAT echo (SatPC32-style).</summary>
    bool SendFireAndForget(string command, int postDelayMs = 50);

    /// <summary>Alias for <see cref="SendFireAndForget"/>.</summary>
    bool SendCommand(string command, int postDelayMs = 50);

    /// <summary>Send and wait for a semicolon-terminated reply (FA;, SA;, RX;, etc.).</summary>
    string? Transact(string command, int postDelayMs = 50);
}
