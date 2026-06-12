namespace OscarWatch.Core.Models;

public enum KeyholeStrategy { Normal, FlippedStart }

public sealed record KeyholePlan(
    KeyholeStrategy Strategy,
    double? FlippedStartAzimuthDeg,
    TimeSpan? PrePositionLeadTime,
    TimeSpan NormalSignalLossWindow,
    TimeSpan FlippedSignalLossWindow);
