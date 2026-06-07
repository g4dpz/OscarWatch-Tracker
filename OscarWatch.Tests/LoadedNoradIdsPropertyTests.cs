// Feature: performance-optimisations, Property 11: LoadedNoradIds accurately reflects the loaded satellite set

using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 11.3**
///
/// Property-based tests verifying that <see cref="PublicOrbitToolsPropagator.LoadedNoradIds"/>
/// accurately reflects the set of satellites loaded via LoadSatellite/RemoveSatellite operations.
/// </summary>
public class LoadedNoradIdsPropertyTests
{
    /// <summary>
    /// A pool of real satellite catalog entries with valid TLE data.
    /// We use these to avoid needing to generate arbitrary valid TLE lines.
    /// </summary>
    private static readonly SatelliteCatalogEntry[] SatellitePool =
    [
        new()
        {
            Name = "ISS (ZARYA)", NoradId = "25544",
            Line1 = "1 25544U 98067A   26141.16510469  .00005835  00000-0  11282-3 0  9994",
            Line2 = "2 25544  51.6328  73.8715 0007529  81.3651 278.8190 15.49291753567565"
        },
        new()
        {
            Name = "AO-07", NoradId = "07530",
            Line1 = "1 07530U 74089B   26141.31992461 -.00000054  00000-0 -48931-4 0  9992",
            Line2 = "2 07530 101.9910 154.2858 0012269 180.6108 191.1977 12.53697584357151"
        },
        new()
        {
            Name = "AO-27", NoradId = "22825",
            Line1 = "1 22825U 93061C   26141.14902361  .00000060  00000-0  39806-4 0  9994",
            Line2 = "2 22825  98.6890 208.5706 0008550 172.0697 188.0622 14.30933961703139"
        },
        new()
        {
            Name = "FO-29", NoradId = "24278",
            Line1 = "1 24278U 96046B   26141.17662052  .00000000  00000-0  34829-4 0  9991",
            Line2 = "2 24278  98.5266 353.7450 0350115 166.3802 194.7089 13.53272915469510"
        },
        new()
        {
            Name = "SO-50", NoradId = "27607",
            Line1 = "1 27607U 02058C   26141.24923057  .00000576  00000-0  85866-4 0  9998",
            Line2 = "2 27607  64.5520 212.3264 0075596 267.4106  91.8345 14.82983020260469"
        },
        new()
        {
            Name = "AO-73", NoradId = "39444",
            Line1 = "1 39444U 13066AE  26140.67569056  .00005251  00000-0  33102-3 0  9992",
            Line2 = "2 39444  97.8265 111.5579 0034836 298.9376  60.8360 15.09093359675511"
        },
        new()
        {
            Name = "IO-86", NoradId = "40931",
            Line1 = "1 40931U 15052B   25151.18580175  .00001241  00000-0  78118-4 0  9996",
            Line2 = "2 40931   6.0006  24.0987 0012733 338.8432  21.1169 14.78805930523159"
        },
        new()
        {
            Name = "AO-91", NoradId = "43017",
            Line1 = "1 43017U 17073E   26141.14920854  .00006846  00000-0  30040-3 0  9994",
            Line2 = "2 43017  97.4737   8.9239 0153707  62.3580 299.3158 15.12168292461300"
        },
    ];

    /// <summary>
    /// Property 11: LoadedNoradIds accurately reflects the loaded satellite set.
    ///
    /// Generates arbitrary sequences of LoadSatellite/RemoveSatellite operations
    /// (encoded as parallel bool[] and int[] arrays), applies each to a
    /// PublicOrbitToolsPropagator, and after all operations asserts LoadedNoradIds
    /// set equals the expected set.
    ///
    /// FsCheck generates the bool[] (isLoad decisions) and int[] (pool indices)
    /// automatically. We zip them together to form the operation sequence.
    /// </summary>
    [Property]
    public bool LoadedNoradIds_reflects_loaded_satellite_set_after_operations(
        bool[] isLoadOps, int[] poolIndices)
    {
        if (isLoadOps is null || isLoadOps.Length == 0 ||
            poolIndices is null || poolIndices.Length == 0)
            return true; // trivially true for empty inputs

        var propagator = new PublicOrbitToolsPropagator();
        var expected = new HashSet<string>(StringComparer.Ordinal);

        var opCount = Math.Min(isLoadOps.Length, poolIndices.Length);

        for (var i = 0; i < opCount; i++)
        {
            // Map arbitrary int to a valid pool index
            var poolIndex = ((poolIndices[i] % SatellitePool.Length) + SatellitePool.Length)
                            % SatellitePool.Length;
            var entry = SatellitePool[poolIndex];

            if (isLoadOps[i])
            {
                propagator.LoadSatellite(entry);
                expected.Add(entry.NoradId);
            }
            else
            {
                propagator.RemoveSatellite(entry.NoradId);
                expected.Remove(entry.NoradId);
            }
        }

        var loadedIds = propagator.LoadedNoradIds;

        // Count must match
        if (loadedIds.Count != expected.Count)
            return false;

        // Contents must match
        var actualSet = new HashSet<string>(loadedIds, StringComparer.Ordinal);
        return actualSet.SetEquals(expected);
    }
}
