namespace OscarWatch.Core.Ft4;

/// <summary>How OscarWatch keys the uplink for FT4 transmit.</summary>
public enum Ft4PttMethod
{
    /// <summary>Play audio only; radio VOX or interface keys from audio.</summary>
    Vox = 0,

    /// <summary>CAT transmit / PTT command on the connected rig.</summary>
    Cat = 1,

    /// <summary>Toggle RTS or DTR on the open CAT serial port.</summary>
    CatPortHandshake = 2,

    /// <summary>Dedicated COM port that only asserts RTS or DTR.</summary>
    SeparateComPort = 3,

    /// <summary>Prompt the operator to key and unkey for each slot.</summary>
    Manual = 4
}

/// <summary>Which handshake line to assert for hardware PTT.</summary>
public enum Ft4PttLine
{
    Rts = 0,
    Dtr = 1
}
