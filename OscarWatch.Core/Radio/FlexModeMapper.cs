namespace OscarWatch.Core.Radio;

/// <summary>Maps OscarWatch / transponder database modes to SmartSDR slice mode tokens.</summary>
public static class FlexModeMapper
{
    public static string? ToSmartSdrMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return null;

        return mode.Trim().ToUpperInvariant() switch
        {
            "USB" => "USB",
            "LSB" => "LSB",
            "CW" => "CW",
            "AM" => "AM",
            "FM" or "FMN" or "NFM" => "FM",
            "DATA-USB" or "PKTUSB" or "DIGU" or "USB-D" => "DIGU",
            "DATA-LSB" or "PKTLSB" or "DIGL" or "LSB-D" => "DIGL",
            "SAM" => "SAM",
            "RTTY" => "RTTY",
            "DFM" => "DFM",
            _ => mode.Trim().ToUpperInvariant()
        };
    }
}
