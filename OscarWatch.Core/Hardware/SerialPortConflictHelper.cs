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
