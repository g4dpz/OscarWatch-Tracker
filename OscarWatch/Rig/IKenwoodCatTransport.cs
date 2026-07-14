namespace OscarWatch.Rig;

internal interface IKenwoodCatTransport : IDisposable
{
    bool IsOpen { get; }
    void Open();

    /// <summary>
    /// Sets and similar commands. TS-2000 usually sends no echo on success; implementations may
    /// treat an immediate <c>?;</c>/<c>E;</c> as failure. Silence means success.
    /// </summary>
    bool SendFireAndForget(string command, int postDelayMs = 50);

    /// <summary>Alias for <see cref="SendFireAndForget"/>.</summary>
    bool SendCommand(string command, int postDelayMs = 50);

    /// <summary>Send and wait for a semicolon-terminated reply (FA;, SA;, RX;, etc.).</summary>
    string? Transact(string command, int postDelayMs = 50);
}
