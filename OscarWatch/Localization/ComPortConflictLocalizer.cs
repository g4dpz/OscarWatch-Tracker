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

        return message;
    }
}
