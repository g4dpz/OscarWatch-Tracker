namespace OscarWatch.Tests;

/// <summary>
/// Configuration record for TS-2000 hardware validation tests.
/// Controls serial port settings and timing parameters for both
/// recording transport (CI) and serial transport (hardware) test execution.
/// </summary>
internal sealed record Ts2000TestConfig
{
    /// <summary>Serial port name (e.g. "COM3" or "/dev/ttyUSB0"). Null when hardware is unavailable.</summary>
    public string? ComPort { get; init; }

    /// <summary>Baud rate for the serial connection. Default matches TS-2000 CAT default.</summary>
    public int BaudRate { get; init; } = 57600;

    /// <summary>Inter-command delay in milliseconds for CAT protocol timing.</summary>
    public int CatDelayMs { get; init; } = 0;

    /// <summary>Delay after SA command to allow satellite mode to settle on the radio.</summary>
    public int SatModeSettlingDelayMs { get; init; } = 0;

    /// <summary>Maximum number of SA; verification retry attempts.</summary>
    public int SatModeRetryCount { get; init; } = 3;

    /// <summary>Delay between SA; verification retry attempts.</summary>
    public int SatModeRetryDelayMs { get; init; } = 0;
}
