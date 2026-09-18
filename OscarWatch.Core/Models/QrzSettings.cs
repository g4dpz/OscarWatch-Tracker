namespace OscarWatch.Core.Models;

public sealed class QrzSettings
{
    /// <summary>When true, OscarWatch Logbook looks up names (and empty grids) from QRZ XML Data.</summary>
    public bool Enabled { get; set; }

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
