namespace OscarWatch.Core.Dxcc;

public readonly record struct DxccMatch(int Dxcc, string Country, string PrimaryPrefix, string CtyName);

public interface IDxccLookupService
{
    string ActiveCountryFilePath { get; }

    DateTime? CountryFileLastWriteUtc { get; }

    bool TryResolve(string? callsign, out DxccMatch match);

    void EnsureLoaded();

    void Reload();

    Task<DxccCountryFileUpdateResult> UpdateCountryFileAsync(CancellationToken cancellationToken = default);
}

public sealed class DxccCountryFileUpdateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? SavedPath { get; init; }
}
