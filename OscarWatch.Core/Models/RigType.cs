namespace OscarWatch.Core.Models;

public enum RigType
{
    None,
    IcomIc910,
    IcomIc9100,
    IcomIc9700,
    IcomIc821h,
    IcomIc705,
    IcomIc706,
    IcomIc706Mkii,
    IcomIc706MkiiG,
    YaesuFt847,
    YaesuFt817,
    YaesuFt818,
    YaesuFt991,
    YaesuFt991a,
    YaesuFtx1,
    KenwoodTs2000,
    /// <summary>SDR application rigctl TCP server (dual-radio downlink only).</summary>
    SdrRigCtlTcp,
    Dummy
}
