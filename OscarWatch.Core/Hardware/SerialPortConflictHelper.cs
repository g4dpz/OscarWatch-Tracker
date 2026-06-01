using OscarWatch.Core.Models;

namespace OscarWatch.Core.Hardware;

public static class SerialPortConflictHelper
{
    public static bool HasConflict(RotatorSettings rotator, RigSettings rig) =>
        TryDescribeConflict(rotator, rig, out _);

    public static bool TryDescribeConflict(
        RotatorSettings rotator,
        RigSettings rig,
        out string message)
    {
        message = "";
        if (!rig.Enabled)
            return false;

        var rotatorPort = rotator.Port?.Trim() ?? "";

        if (rig.DualRadioEnabled)
        {
            var downPort = rig.Downlink.Port?.Trim() ?? "";
            var upPort = rig.Uplink.Port?.Trim() ?? "";

            if (downPort.Length > 0 && upPort.Length > 0
                && string.Equals(downPort, upPort, StringComparison.OrdinalIgnoreCase))
            {
                message =
                    $"Downlink and uplink radios both use {downPort}. Use different COM ports for each radio.";
                return true;
            }

            if (!rotator.Enabled || rotatorPort.Length == 0)
                return false;

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

            return false;
        }

        if (!rotator.Enabled)
            return false;

        if (rig.Type == RigType.Dummy)
            return false;
        var rigPort = rig.Port?.Trim() ?? "";
        if (rotatorPort.Length == 0 || rigPort.Length == 0)
            return false;

        if (!string.Equals(rotatorPort, rigPort, StringComparison.OrdinalIgnoreCase))
            return false;

        message =
            $"Rotator and radio both use {rotatorPort}. Use different COM ports or disable one device.";
        return true;
    }
}
