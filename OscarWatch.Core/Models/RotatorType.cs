namespace OscarWatch.Core.Models;

public enum RotatorType
{
    YaesuGs232,
    EasyComm,
    Spid,
    Saebrt,
    /// <summary>OZ9AAR Ultimate Rotator Controller over TCP/JSON (POLL / GOTO).</summary>
    UrcTcp,
    /// <summary>
    /// Green Heron RT-21 Az-El: two independent DCU-1 serial links (azimuth and elevation COM ports).
    /// </summary>
    GreenHeronRt21
}
