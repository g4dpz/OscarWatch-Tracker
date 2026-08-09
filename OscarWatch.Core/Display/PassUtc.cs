namespace OscarWatch.Core.Display;

/// <summary>
/// Orbit/pass times are stored as UTC. Unspecified Kind values are treated as UTC
/// (common from propagation libraries) so comparisons with <see cref="DateTime.UtcNow"/> stay correct.
/// </summary>
public static class PassUtc
{
    public static DateTime Normalize(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
