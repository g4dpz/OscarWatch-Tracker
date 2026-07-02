namespace OscarWatch.Core.Hardware;

public enum SerialPortConnectErrorKind
{
    None,
    PortNotFound,
    PortBusy,
    DualSamePort,
    Generic,
}

public static class SerialPortConnectErrorHelper
{
    public const string EndpointDownlink = "Downlink";
    public const string EndpointUplink = "Uplink";

    public static bool TryDescribeDualSamePort(
        string? downPort,
        string? upPort,
        out string sharedPort)
    {
        sharedPort = "";
        var down = downPort?.Trim() ?? "";
        var up = upPort?.Trim() ?? "";
        if (down.Length == 0 || up.Length == 0)
            return false;

        if (!string.Equals(down, up, StringComparison.OrdinalIgnoreCase))
            return false;

        sharedPort = down;
        return true;
    }

    public static SerialPortConnectErrorKind Classify(Exception ex)
    {
        var message = FlattenMessages(ex);
        if (IsPortNotFoundMessage(message))
            return SerialPortConnectErrorKind.PortNotFound;

        if (IsPortBusyMessage(message))
            return SerialPortConnectErrorKind.PortBusy;

        return SerialPortConnectErrorKind.Generic;
    }

    public static string ToEnglish(
        SerialPortConnectErrorKind kind,
        string port,
        string? endpointLabel = null)
    {
        switch (kind)
        {
            case SerialPortConnectErrorKind.PortNotFound:
                return endpointLabel is null
                    ? $"Serial port not found ({port}). Check the USB cable and refresh the port list."
                    : $"{endpointLabel} serial port not found ({port}). Check the USB cable and refresh the port list.";
            case SerialPortConnectErrorKind.PortBusy:
                return endpointLabel is null
                    ? $"Serial port in use ({port}). Close other CAT programs or choose a different port."
                    : $"{endpointLabel} serial port in use ({port}). Close other CAT programs or choose a different port.";
            case SerialPortConnectErrorKind.DualSamePort:
                return
                    $"Downlink and uplink radios both use {port}. Use different COM ports for each radio.";
            default:
                return port.Length > 0 ? port : "Connection failed";
        }
    }

    private static bool IsPortNotFoundMessage(string message) =>
        message.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
        || message.Contains("could not find file", StringComparison.OrdinalIgnoreCase)
        || message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
        || message.Contains("Port not found", StringComparison.OrdinalIgnoreCase)
        || message.Contains("The system cannot find the file", StringComparison.OrdinalIgnoreCase);

    private static bool IsPortBusyMessage(string message) =>
        message.Contains("Device or resource busy", StringComparison.OrdinalIgnoreCase)
        || message.Contains("being used by another", StringComparison.OrdinalIgnoreCase)
        || message.Contains("Access to the port", StringComparison.OrdinalIgnoreCase)
            && message.Contains("denied", StringComparison.OrdinalIgnoreCase)
            && !IsPortNotFoundMessage(message);

    private static string FlattenMessages(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
            parts.Add(current.Message);

        return string.Join(" | ", parts);
    }
}
