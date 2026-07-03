namespace OscarWatch.Core.Models;

public sealed class CloudlogStationProfile
{
    public int StationId { get; init; }

    public string ProfileName { get; init; } = "";

    public string Callsign { get; init; } = "";

    public string GridSquare { get; init; } = "";

    public bool IsActive { get; init; }

    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(Callsign)
            ? ProfileName
            : $"{ProfileName} ({Callsign})";
}

public sealed class CloudlogStationProfilesResult
{
    public bool Ok { get; init; }

    public IReadOnlyList<CloudlogStationProfile> Profiles { get; init; } = [];

    public string? ErrorMessage { get; init; }

    public static CloudlogStationProfilesResult Success(IReadOnlyList<CloudlogStationProfile> profiles) =>
        new() { Ok = true, Profiles = profiles };

    public static CloudlogStationProfilesResult Failed(string message) =>
        new() { Ok = false, ErrorMessage = message };
}
