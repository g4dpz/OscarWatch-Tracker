using OscarWatch.Core.Models;

namespace OscarWatch.Core.Hardware;

public static class SerialPortConflictHelper
{
    public static bool HasConflict(RotatorSettings rotator, RigSettings rig, GpsSettings? gps = null) =>
        TryDescribeConflict(rotator, rig, gps, out _);

    public static bool TryDescribeConflict(
        RotatorSettings rotator,
        RigSettings rig,
        out string message) =>
        TryDescribeConflict(rotator, rig, gps: null, out message);

    public static bool TryDescribeConflict(
        RotatorSettings rotator,
        RigSettings rig,
        GpsSettings? gps,
        out string message)
    {
        message = "";
        var gpsPort = gps is { Enabled: true, ConnectionKind: GpsConnectionKind.Serial }
            ? gps.Port?.Trim() ?? ""
            : "";

        var rotatorPorts = GetRotatorSerialPorts(rotator);

        if (rotator.Enabled
            && rotator.UsesDualSerialPorts
            && rotatorPorts.Count >= 2
            && string.Equals(rotatorPorts[0], rotatorPorts[1], StringComparison.OrdinalIgnoreCase))
        {
            message =
                $"Rotator azimuth and elevation both use {rotatorPorts[0]}. Use different COM ports for each axis.";
            return true;
        }

        if (rig.Enabled)
        {
            if (rig.DualRadioEnabled)
            {
                var downPort = rig.Downlink.Port?.Trim() ?? "";
                var upPort = rig.Uplink.Type == RigType.Dummy ? "" : rig.Uplink.Port?.Trim() ?? "";

                if (downPort.Length > 0 && upPort.Length > 0
                    && string.Equals(downPort, upPort, StringComparison.OrdinalIgnoreCase))
                {
                    message =
                        $"Downlink and uplink radios both use {downPort}. Use different COM ports for each radio.";
                    return true;
                }

                if (TryDescribeGpsConflict(gpsPort, downPort, "downlink radio", out message)
                    || TryDescribeGpsConflict(gpsPort, upPort, "uplink radio", out message))
                    return true;

                foreach (var rotatorPort in rotatorPorts)
                {
                    if (downPort.Length > 0
                        && string.Equals(rotatorPort, downPort, StringComparison.OrdinalIgnoreCase))
                    {
                        message =
                            $"Rotator and downlink radio both use {rotatorPort}. Use different COM ports or disable one device.";
                        return true;
                    }

                    if (upPort.Length > 0
                        && string.Equals(rotatorPort, upPort, StringComparison.OrdinalIgnoreCase))
                    {
                        message =
                            $"Rotator and uplink radio both use {rotatorPort}. Use different COM ports or disable one device.";
                        return true;
                    }
                }

                return false;
            }

            if (TryDescribeGpsConflict(gpsPort, rig.Port?.Trim() ?? "", "radio", out message))
                return true;

            if (rotatorPorts.Count == 0)
                return false;

            if (rig.Type == RigType.Dummy)
                return false;

            var rigPort = rig.Port?.Trim() ?? "";
            if (rigPort.Length == 0)
                return false;

            foreach (var rotatorPort in rotatorPorts)
            {
                if (!string.Equals(rotatorPort, rigPort, StringComparison.OrdinalIgnoreCase))
                    continue;

                message =
                    $"Rotator and radio both use {rotatorPort}. Use different COM ports or disable one device.";
                return true;
            }

            return false;
        }

        if (gpsPort.Length == 0 || rotatorPorts.Count == 0)
            return false;

        foreach (var rotatorPort in rotatorPorts)
        {
            if (!string.Equals(gpsPort, rotatorPort, StringComparison.OrdinalIgnoreCase))
                continue;

            message =
                $"GPS and rotator both use {gpsPort}. Use different COM ports or disable one device.";
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when an FT4 separate-PTT COM port would steal the radio, rotator, or GPS serial port.
    /// </summary>
    public static bool TryDescribeFt4SeparatePttConflict(
        string? separatePttPort,
        RigSettings rig,
        RotatorSettings rotator,
        GpsSettings? gps,
        out string message)
    {
        message = "";
        var ptt = separatePttPort?.Trim() ?? "";
        if (ptt.Length == 0)
            return false;

        foreach (var (port, label) in EnumerateOccupiedPorts(rig, rotator, gps))
        {
            if (!string.Equals(ptt, port, StringComparison.OrdinalIgnoreCase))
                continue;

            message = label switch
            {
                "radio" =>
                    $"FT4 separate PTT and radio both use {ptt}. Use different COM ports or disable one device.",
                "downlink radio" =>
                    $"FT4 separate PTT and downlink radio both use {ptt}. Use different COM ports or disable one device.",
                "uplink radio" =>
                    $"FT4 separate PTT and uplink radio both use {ptt}. Use different COM ports or disable one device.",
                "rotator" =>
                    $"FT4 separate PTT and rotator both use {ptt}. Use different COM ports or disable one device.",
                "GPS" =>
                    $"FT4 separate PTT and GPS both use {ptt}. Use different COM ports or disable one device.",
                _ =>
                    $"FT4 separate PTT and {label} both use {ptt}. Use different COM ports or disable one device."
            };
            return true;
        }

        return false;
    }

    private static IEnumerable<(string Port, string Label)> EnumerateOccupiedPorts(
        RigSettings rig,
        RotatorSettings rotator,
        GpsSettings? gps)
    {
        if (rig.Enabled)
        {
            if (rig.DualRadioEnabled)
            {
                var down = rig.Downlink.Port?.Trim() ?? "";
                if (down.Length > 0 && rig.Downlink.Type != RigType.Dummy)
                    yield return (down, "downlink radio");

                var up = rig.Uplink.Port?.Trim() ?? "";
                if (up.Length > 0 && rig.Uplink.Type != RigType.Dummy)
                    yield return (up, "uplink radio");
            }
            else if (rig.Type != RigType.Dummy)
            {
                var port = rig.Port?.Trim() ?? "";
                if (port.Length > 0)
                    yield return (port, "radio");
            }
        }

        foreach (var rotatorPort in GetRotatorSerialPorts(rotator))
            yield return (rotatorPort, "rotator");

        if (gps is { Enabled: true, ConnectionKind: GpsConnectionKind.Serial })
        {
            var gpsPort = gps.Port?.Trim() ?? "";
            if (gpsPort.Length > 0)
                yield return (gpsPort, "GPS");
        }
    }

    /// <summary>COM ports when the rotator is enabled and using local serial transport.</summary>
    private static IReadOnlyList<string> GetRotatorSerialPorts(RotatorSettings rotator)
    {
        if (!rotator.Enabled || !rotator.UsesSerialPort)
            return Array.Empty<string>();

        return rotator.GetConfiguredSerialPorts();
    }

    private static bool TryDescribeGpsConflict(
        string gpsPort,
        string devicePort,
        string deviceLabel,
        out string message)
    {
        message = "";
        if (gpsPort.Length == 0 || devicePort.Length == 0)
            return false;

        if (!string.Equals(gpsPort, devicePort, StringComparison.OrdinalIgnoreCase))
            return false;

        message =
            $"GPS and {deviceLabel} both use {gpsPort}. Use different COM ports or disable one device.";
        return true;
    }
}
