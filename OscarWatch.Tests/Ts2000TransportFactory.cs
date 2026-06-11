using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Produces the correct <see cref="IKenwoodCatTransport"/> based on environment configuration.
/// Used by test classes to run against either the recording transport (CI) or serial transport (hardware).
/// </summary>
internal static class Ts2000TransportFactory
{
    private const int DefaultBaudRate = 57600;
    private const int DefaultCatDelayMs = 50;

    /// <summary>True if <c>TS2000_COM_PORT</c> is set and non-empty.</summary>
    public static bool IsHardwareAvailable { get; } =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TS2000_COM_PORT"));

    /// <summary>The COM port value from the <c>TS2000_COM_PORT</c> environment variable.</summary>
    public static string? ComPort { get; } =
        Environment.GetEnvironmentVariable("TS2000_COM_PORT");

    /// <summary>Baud rate parsed from <c>TS2000_BAUD_RATE</c> or default 57600.</summary>
    public static int BaudRate { get; } = ParseIntEnvVar("TS2000_BAUD_RATE", DefaultBaudRate);

    /// <summary>Inter-command delay in milliseconds parsed from <c>TS2000_CAT_DELAY_MS</c> or default 50.</summary>
    public static int CatDelayMs { get; } = ParseIntEnvVar("TS2000_CAT_DELAY_MS", DefaultCatDelayMs);

    /// <summary>
    /// Creates a <see cref="RecordingKenwoodCatTransport"/> with configurable initial state.
    /// </summary>
    public static RecordingKenwoodCatTransport CreateRecordingTransport(
        long faHz = 435_750_000,
        long fbHz = 145_900_000,
        bool satelliteStatusOn = true)
    {
        return new RecordingKenwoodCatTransport
        {
            FaHz = faHz,
            FbHz = fbHz,
            SatelliteStatusOn = satelliteStatusOn
        };
    }

    /// <summary>
    /// Creates a <see cref="KenwoodCatTransport"/> using the configured COM port and baud rate.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <c>TS2000_COM_PORT</c> is not configured.</exception>
    public static KenwoodCatTransport CreateSerialTransport()
    {
        if (!IsHardwareAvailable)
            throw new InvalidOperationException(
                "Cannot create serial transport: TS2000_COM_PORT environment variable is not set.");

        return new KenwoodCatTransport(ComPort!, BaudRate);
    }

    private static int ParseIntEnvVar(string variableName, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
