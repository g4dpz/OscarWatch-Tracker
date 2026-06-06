namespace OscarWatch.Core.Models;

public sealed class HamsAtSettings
{
    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = "";

    public int RefreshIntervalMinutes { get; set; } = 10;
}
