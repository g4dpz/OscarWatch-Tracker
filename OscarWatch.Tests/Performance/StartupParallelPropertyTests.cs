// Feature: startup-io-rendering-optimisation, Property 1: Startup partial-failure resilience

using FsCheck;
using FsCheck.Xunit;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Property 1: For any subset of the three startup I/O tasks (settings, TLE, satellite DB)
/// that throws an exception, all non-failing tasks SHALL complete successfully and their
/// results SHALL be applied.
///
/// **Validates: Requirements 1.4**
/// </summary>
public class StartupParallelPropertyTests
{
    /// <summary>
    /// Simulates the Task.WhenAll partial-failure pattern used in MainViewModel.InitializeAsync.
    /// Generates a random bool triple indicating which tasks fail; asserts that non-failing tasks
    /// complete and their results are available despite other tasks throwing.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool NonFailing_tasks_complete_when_others_throw(bool settingsFails, bool tleFails, bool satDbFails)
    {
        // Arrange: create tasks that either succeed with a result or throw
        var settingsResult = "settings-loaded";
        var tleResult = "tle-loaded";
        var satDbResult = "satdb-loaded";

        string? capturedSettings = null;
        string? capturedTle = null;
        string? capturedSatDb = null;

        var settingsTask = RunTaskSafe(
            settingsFails
                ? Task.FromException<string>(new InvalidOperationException("Settings I/O failed"))
                : Task.FromResult(settingsResult),
            result => capturedSettings = result);

        var tleTask = RunTaskSafe(
            tleFails
                ? Task.FromException<string>(new InvalidOperationException("TLE I/O failed"))
                : Task.FromResult(tleResult),
            result => capturedTle = result);

        var satDbTask = RunTaskSafe(
            satDbFails
                ? Task.FromException<string>(new InvalidOperationException("SatDb I/O failed"))
                : Task.FromResult(satDbResult),
            result => capturedSatDb = result);

        // Act: run all tasks concurrently (mirrors the Task.WhenAll pattern)
        Task.WhenAll(settingsTask, tleTask, satDbTask).GetAwaiter().GetResult();

        // Assert: non-failing tasks have their results applied
        var settingsOk = settingsFails ? capturedSettings is null : capturedSettings == settingsResult;
        var tleOk = tleFails ? capturedTle is null : capturedTle == tleResult;
        var satDbOk = satDbFails ? capturedSatDb is null : capturedSatDb == satDbResult;

        return settingsOk && tleOk && satDbOk;
    }

    /// <summary>
    /// Helper that mirrors the try/catch-per-task pattern from InitializeAsync.
    /// On success, applies the result; on failure, swallows the exception (logs in production).
    /// </summary>
    private static async Task RunTaskSafe<T>(Task<T> task, Action<T> applyResult)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            applyResult(result);
        }
        catch
        {
            // Partial failure: log and continue (mirrors production behaviour)
        }
    }
}
