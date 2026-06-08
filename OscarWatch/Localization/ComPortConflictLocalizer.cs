namespace OscarWatch.Localization;

public static class ComPortConflictLocalizer
{
    public static string Localize(string message, ILocalizationService l)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        const string dualPrefix = "Downlink and uplink radios both use ";
        if (message.StartsWith(dualPrefix, StringComparison.Ordinal)
            && message.EndsWith(". Use different COM ports for each radio.", StringComparison.Ordinal))
        {
            var port = message[dualPrefix.Length..^". Use different COM ports for each radio.".Length];
            return l.Get("ComPort.DualRadioSamePort", port);
        }

        const string rotatorDownlinkPrefix = "Rotator and downlink radio both use ";
        if (message.StartsWith(rotatorDownlinkPrefix, StringComparison.Ordinal)
            && message.EndsWith(". Use different COM ports or disable one device.", StringComparison.Ordinal))
        {
            var port = message[rotatorDownlinkPrefix.Length..^". Use different COM ports or disable one device.".Length];
            return l.Get("ComPort.RotatorAndDownlink", port);
        }

        const string rotatorUplinkPrefix = "Rotator and uplink radio both use ";
        if (message.StartsWith(rotatorUplinkPrefix, StringComparison.Ordinal)
            && message.EndsWith(". Use different COM ports or disable one device.", StringComparison.Ordinal))
        {
            var port = message[rotatorUplinkPrefix.Length..^". Use different COM ports or disable one device.".Length];
            return l.Get("ComPort.RotatorAndUplink", port);
        }

        const string rotatorRadioPrefix = "Rotator and radio both use ";
        if (message.StartsWith(rotatorRadioPrefix, StringComparison.Ordinal)
            && message.EndsWith(". Use different COM ports or disable one device.", StringComparison.Ordinal))
        {
            var port = message[rotatorRadioPrefix.Length..^". Use different COM ports or disable one device.".Length];
            return l.Get("ComPort.RotatorAndRadio", port);
        }

        const string gpsRotatorPrefix = "GPS and rotator both use ";
        if (message.StartsWith(gpsRotatorPrefix, StringComparison.Ordinal)
            && message.EndsWith(". Use different COM ports or disable one device.", StringComparison.Ordinal))
        {
            var port = message[gpsRotatorPrefix.Length..^". Use different COM ports or disable one device.".Length];
            return l.Get("ComPort.GpsAndRotator", port);
        }

        const string gpsRadioPrefix = "GPS and radio both use ";
        if (message.StartsWith(gpsRadioPrefix, StringComparison.Ordinal)
            && message.EndsWith(". Use different COM ports or disable one device.", StringComparison.Ordinal))
        {
            var port = message[gpsRadioPrefix.Length..^". Use different COM ports or disable one device.".Length];
            return l.Get("ComPort.GpsAndRadio", port);
        }

        const string gpsDownlinkPrefix = "GPS and downlink radio both use ";
        if (message.StartsWith(gpsDownlinkPrefix, StringComparison.Ordinal)
            && message.EndsWith(". Use different COM ports or disable one device.", StringComparison.Ordinal))
        {
            var port = message[gpsDownlinkPrefix.Length..^". Use different COM ports or disable one device.".Length];
            return l.Get("ComPort.GpsAndDownlink", port);
        }

        const string gpsUplinkPrefix = "GPS and uplink radio both use ";
        if (message.StartsWith(gpsUplinkPrefix, StringComparison.Ordinal)
            && message.EndsWith(". Use different COM ports or disable one device.", StringComparison.Ordinal))
        {
            var port = message[gpsUplinkPrefix.Length..^". Use different COM ports or disable one device.".Length];
            return l.Get("ComPort.GpsAndUplink", port);
        }

        return message;
    }
}
