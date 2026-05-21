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
        if (!rotator.Enabled || !rig.Enabled)
            return false;

        if (rig.Type == RigType.Dummy)
            return false;

        var rotatorPort = rotator.Port?.Trim() ?? "";
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
