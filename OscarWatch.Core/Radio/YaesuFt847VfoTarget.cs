namespace OscarWatch.Core.Radio;

/// <summary>FT-847 CAT VFO target (opcode high nibble when satellite mode is on).</summary>
public enum YaesuFt847VfoTarget
{
    Main = 0x00,
    SatRx = 0x10,
    SatTx = 0x20
}
