namespace OscarWatch.Core.Models;

public sealed class VoiceAnnouncementSettings
{
    public bool Enabled { get; set; }
    public double AnnounceElevationDeg { get; set; } = -3.0;
    /// <summary>Platform-specific voice id; empty uses the OS default voice.</summary>
    public string VoiceName { get; set; } = "";
}
