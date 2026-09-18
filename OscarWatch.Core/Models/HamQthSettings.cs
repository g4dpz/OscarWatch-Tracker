namespace OscarWatch.Core.Models;

public sealed class HamQthSettings
{
    /// <summary>When true, OscarWatch Logbook looks up names (and empty grids) from HamQTH.</summary>
    public bool Enabled { get; set; }

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
