namespace OscarWatch.Tests;

/// <summary>Builds recording CAT transports for TS-2000 unit tests (no live hardware).</summary>
internal static class Ts2000TransportFactory
{
    /// <summary>
    /// Creates a <see cref="RecordingKenwoodCatTransport"/> with configurable initial state.
    /// </summary>
    public static RecordingKenwoodCatTransport CreateRecordingTransport(
        long faHz = 435_750_000,
        long fbHz = 145_900_000,
        bool satelliteStatusOn = true) =>
        new()
        {
            FaHz = faHz,
            FbHz = fbHz,
            SatelliteStatusOn = satelliteStatusOn
        };
}
