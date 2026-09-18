namespace OscarWatch.Core.HamQth;

public sealed class HamQthConnectionTestResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public static HamQthConnectionTestResult Success() => new() { Ok = true };

    public static HamQthConnectionTestResult Failed(string message) => new()
    {
        Ok = false,
        ErrorMessage = message
    };
}
