using System.Collections.ObjectModel;
using OscarWatch.Core.Models;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

/// <summary>
/// Unit tests verifying PruneExpiredPasses for-loop algorithm behaviour.
/// The algorithm is replicated here as a standalone helper to test in isolation
/// without requiring access to MainViewModel's private method.
/// </summary>
public sealed class PruneExpiredPassesTests
{
    /// <summary>
    /// Replicates the PruneExpiredPasses for-loop algorithm from MainViewModel
    /// so we can test the pruning logic in isolation.
    /// </summary>
    private static void PruneExpiredPasses(ObservableCollection<IPassListItem> passes, DateTime utcNow)
    {
        var removedAny = false;

        for (var i = passes.Count - 1; i >= 0; i--)
        {
            if (passes[i] is PassRowViewModel p && p.LosUtc < utcNow)
            {
                passes.RemoveAt(i);
                removedAny = true;
            }
        }

        if (!removedAny)
            return;

        // Remove orphaned day headers (reverse scan)
        for (var i = passes.Count - 1; i >= 0; i--)
        {
            if (passes[i] is PassDayHeaderViewModel
                && (i + 1 >= passes.Count || passes[i + 1] is PassDayHeaderViewModel))
                passes.RemoveAt(i);
        }
    }

    #region Helpers

    private static PassRowViewModel MakePass(string name, DateTime losUtc) => new()
    {
        Source = new PassInfo
        {
            SatelliteName = name,
            NoradId = "00000",
            AosUtc = losUtc.AddMinutes(-10),
            LosUtc = losUtc
        },
        SatelliteName = name,
        NoradId = "00000",
        LosUtc = losUtc,
        AosUtc = losUtc.AddMinutes(-10),
        MaxElevationUtc = losUtc.AddMinutes(-5)
    };

    private static PassDayHeaderViewModel MakeHeader(string label) => new()
    {
        DateLabel = label
    };

    #endregion

    [Fact]
    public void Expired_passes_are_removed_and_future_passes_preserved_in_order()
    {
        // Arrange: mix of expired and future passes with day headers
        var now = new DateTime(2024, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        var header1 = MakeHeader("14 July 2024");
        var expired1 = MakePass("ISS", now.AddMinutes(-60));      // expired
        var expired2 = MakePass("SO-50", now.AddMinutes(-30));    // expired

        var header2 = MakeHeader("15 July 2024");
        var future1 = MakePass("AO-91", now.AddMinutes(30));      // future
        var future2 = MakePass("FO-99", now.AddMinutes(90));      // future

        var passes = new ObservableCollection<IPassListItem>
        {
            header1, expired1, expired2,
            header2, future1, future2
        };

        // Act
        PruneExpiredPasses(passes, now);

        // Assert: expired passes removed, future passes preserved in original order
        Assert.Equal(3, passes.Count);
        Assert.Same(header2, passes[0]);
        Assert.Same(future1, passes[1]);
        Assert.Same(future2, passes[2]);
    }

    [Fact]
    public void Orphaned_day_headers_are_removed_after_all_their_passes_expire()
    {
        // Arrange: a header with only expired passes beneath it
        var now = new DateTime(2024, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        var header1 = MakeHeader("14 July 2024");
        var expired1 = MakePass("ISS", now.AddMinutes(-60));

        var header2 = MakeHeader("15 July 2024");
        var future1 = MakePass("AO-91", now.AddMinutes(30));

        var passes = new ObservableCollection<IPassListItem>
        {
            header1, expired1,
            header2, future1
        };

        // Act
        PruneExpiredPasses(passes, now);

        // Assert: header1 is orphaned (no passes beneath it) so it's removed
        Assert.Equal(2, passes.Count);
        Assert.Same(header2, passes[0]);
        Assert.Same(future1, passes[1]);
    }

    [Fact]
    public void Consecutive_orphaned_headers_are_all_removed()
    {
        // Arrange: two headers in a row after their passes expire
        var now = new DateTime(2024, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        var header1 = MakeHeader("13 July 2024");
        var expired1 = MakePass("ISS", now.AddMinutes(-120));

        var header2 = MakeHeader("14 July 2024");
        var expired2 = MakePass("SO-50", now.AddMinutes(-60));

        var header3 = MakeHeader("15 July 2024");
        var future1 = MakePass("AO-91", now.AddMinutes(30));

        var passes = new ObservableCollection<IPassListItem>
        {
            header1, expired1,
            header2, expired2,
            header3, future1
        };

        // Act
        PruneExpiredPasses(passes, now);

        // Assert: both orphaned headers removed, only header3 + future pass remain
        Assert.Equal(2, passes.Count);
        Assert.Same(header3, passes[0]);
        Assert.Same(future1, passes[1]);
    }

    [Fact]
    public void No_removals_when_all_passes_are_future()
    {
        // Arrange: all passes are in the future — zero-allocation path
        var now = new DateTime(2024, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        var header1 = MakeHeader("15 July 2024");
        var future1 = MakePass("ISS", now.AddMinutes(30));
        var future2 = MakePass("SO-50", now.AddMinutes(60));

        var header2 = MakeHeader("16 July 2024");
        var future3 = MakePass("AO-91", now.AddMinutes(120));

        var passes = new ObservableCollection<IPassListItem>
        {
            header1, future1, future2,
            header2, future3
        };

        // Act
        PruneExpiredPasses(passes, now);

        // Assert: nothing removed — collection unchanged
        Assert.Equal(5, passes.Count);
        Assert.Same(header1, passes[0]);
        Assert.Same(future1, passes[1]);
        Assert.Same(future2, passes[2]);
        Assert.Same(header2, passes[3]);
        Assert.Same(future3, passes[4]);
    }

    [Fact]
    public void Partial_removal_preserves_remaining_pass_ordering()
    {
        // Arrange: interleaved expired/future under same header
        var now = new DateTime(2024, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        var header = MakeHeader("15 July 2024");
        var expired = MakePass("ISS", now.AddMinutes(-10));       // expired
        var future1 = MakePass("SO-50", now.AddMinutes(20));      // future
        var future2 = MakePass("AO-91", now.AddMinutes(40));      // future

        var passes = new ObservableCollection<IPassListItem>
        {
            header, expired, future1, future2
        };

        // Act
        PruneExpiredPasses(passes, now);

        // Assert: header still valid (has passes after it), order preserved
        Assert.Equal(3, passes.Count);
        Assert.Same(header, passes[0]);
        Assert.Same(future1, passes[1]);
        Assert.Same(future2, passes[2]);
    }

    [Fact]
    public void Empty_collection_does_not_throw()
    {
        var now = new DateTime(2024, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        var passes = new ObservableCollection<IPassListItem>();

        // Act — should not throw
        PruneExpiredPasses(passes, now);

        // Assert
        Assert.Empty(passes);
    }

    [Fact]
    public void Trailing_header_at_end_is_removed_when_orphaned()
    {
        // Arrange: header at end of list with no passes after it
        var now = new DateTime(2024, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        var header1 = MakeHeader("15 July 2024");
        var future1 = MakePass("ISS", now.AddMinutes(30));

        var header2 = MakeHeader("16 July 2024");
        var expired = MakePass("SO-50", now.AddMinutes(-10)); // expired, last under header2

        var passes = new ObservableCollection<IPassListItem>
        {
            header1, future1,
            header2, expired
        };

        // Act
        PruneExpiredPasses(passes, now);

        // Assert: header2 orphaned (at end of list), removed
        Assert.Equal(2, passes.Count);
        Assert.Same(header1, passes[0]);
        Assert.Same(future1, passes[1]);
    }
}
