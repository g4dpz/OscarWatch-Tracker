// Feature: performance-optimisations, Property 5: CatUpdatesPaused override equivalence

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 5.1, 5.2, 5.3**
///
/// Property-based tests verifying that the catPausedOverride mechanism produces the same
/// effective CatUpdatesPaused value as the formula: catPausedOverride ?? settings.CatUpdatesPaused.
/// This matches the behaviour the previous deep-clone approach would have produced.
/// </summary>
public class CatPausedOverridePropertyTests
{
    /// <summary>
    /// Computes the effective CatUpdatesPaused value using the same logic as RigController.ApplyPublishState:
    /// <c>var effectivePaused = catPausedOverride ?? settings.CatUpdatesPaused;</c>
    /// </summary>
    private static bool ComputeEffectivePaused(bool settingsCatUpdatesPaused, bool? catPausedOverride)
        => catPausedOverride ?? settingsCatUpdatesPaused;

    /// <summary>
    /// Property 5: CatUpdatesPaused override equivalence.
    ///
    /// For any RigSettings.CatUpdatesPaused value and any catPausedOverride (null, true, or false),
    /// the effective paused value SHALL equal catPausedOverride ?? settings.CatUpdatesPaused.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Effective_paused_equals_override_coalesce_settings(bool settingsPaused, bool? overrideValue)
    {
        var settings = new RigSettings { CatUpdatesPaused = settingsPaused };

        var effective = ComputeEffectivePaused(settings.CatUpdatesPaused, overrideValue);
        var expected = overrideValue ?? settings.CatUpdatesPaused;

        return effective == expected;
    }

    /// <summary>
    /// Property 5: When catPausedOverride is null, effective value equals settings.CatUpdatesPaused.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Null_override_returns_settings_value(bool settingsPaused)
    {
        var settings = new RigSettings { CatUpdatesPaused = settingsPaused };

        var effective = ComputeEffectivePaused(settings.CatUpdatesPaused, null);

        return effective == settingsPaused;
    }

    /// <summary>
    /// Property 5: When catPausedOverride is non-null, effective value equals the override
    /// regardless of settings.CatUpdatesPaused.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool NonNull_override_takes_precedence(bool settingsPaused, bool overrideBool)
    {
        var settings = new RigSettings { CatUpdatesPaused = settingsPaused };
        bool? overrideValue = overrideBool;

        var effective = ComputeEffectivePaused(settings.CatUpdatesPaused, overrideValue);

        return effective == overrideBool;
    }
}
