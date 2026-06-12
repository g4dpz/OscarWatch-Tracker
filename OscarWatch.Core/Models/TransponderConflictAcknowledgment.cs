namespace OscarWatch.Core.Models;

/// <summary>
/// User confirmed keeping their local transponder mode instead of the published version.
/// Re-shown when either the local or published fingerprint changes.
/// </summary>
public sealed class TransponderConflictAcknowledgment
{
    public string Key { get; set; } = "";
    public string LocalFingerprint { get; set; } = "";
    public string RemoteFingerprint { get; set; } = "";
}
