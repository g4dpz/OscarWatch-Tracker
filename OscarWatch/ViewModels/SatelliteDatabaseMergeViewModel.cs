using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Services;

namespace OscarWatch.ViewModels;

public partial class SatelliteDatabaseMergeViewModel : ViewModelBase
{
    public SatelliteDatabaseMergeViewModel(SatelliteDatabaseMergePlan plan)
    {
        Plan = plan;
        SummaryText = BuildSummary(plan);

        foreach (var item in plan.NewSatellites)
        {
            NewSatellites.Add(new MergeAdditionItem(
                item.Key,
                $"{item.Entry.Name} ({item.Entry.Modes.Count} mode{(item.Entry.Modes.Count == 1 ? "" : "s")})"));
        }

        foreach (var item in plan.NewModes)
        {
            NewModes.Add(new MergeAdditionItem(
                item.Key,
                $"{item.SatelliteName} · {item.Mode.Type}"));
        }

        foreach (var item in plan.Conflicts)
        {
            Conflicts.Add(new MergeConflictItem(
                item.Key,
                item.SatelliteName,
                item.ModeType,
                SatelliteDatabaseMerger.DescribeMode(item.LocalMode),
                SatelliteDatabaseMerger.DescribeMode(item.RemoteMode)));
        }
    }

    public SatelliteDatabaseMergePlan Plan { get; }

    public string SummaryText { get; }

    public bool HasNewSatellites => NewSatellites.Count > 0;
    public bool HasNewModes => NewModes.Count > 0;
    public bool HasConflicts => Conflicts.Count > 0;

    public ObservableCollection<MergeAdditionItem> NewSatellites { get; } = [];
    public ObservableCollection<MergeAdditionItem> NewModes { get; } = [];
    public ObservableCollection<MergeConflictItem> Conflicts { get; } = [];

    public SatelliteDatabaseMergeSelection BuildSelection()
    {
        var selection = new SatelliteDatabaseMergeSelection();
        foreach (var item in NewSatellites.Where(i => i.IsSelected))
            selection.AcceptedNewSatelliteKeys.Add(item.Key);

        foreach (var item in NewModes.Where(i => i.IsSelected))
            selection.AcceptedNewModeKeys.Add(item.Key);

        foreach (var item in Conflicts.Where(i => i.UseRemote))
            selection.AcceptRemoteConflictKeys.Add(item.Key);

        return selection;
    }

    public bool HasSelectedChanges()
    {
        if (NewSatellites.Any(i => i.IsSelected) || NewModes.Any(i => i.IsSelected))
            return true;

        return Conflicts.Any(i => i.UseRemote);
    }

    private static string BuildSummary(SatelliteDatabaseMergePlan plan)
    {
        var parts = new List<string>();
        if (plan.NewSatellites.Count > 0)
            parts.Add($"{plan.NewSatellites.Count} new satellite{(plan.NewSatellites.Count == 1 ? "" : "s")}");
        if (plan.NewModes.Count > 0)
            parts.Add($"{plan.NewModes.Count} new mode{(plan.NewModes.Count == 1 ? "" : "s")}");
        if (plan.Conflicts.Count > 0)
            parts.Add($"{plan.Conflicts.Count} conflict{(plan.Conflicts.Count == 1 ? "" : "s")}");

        return parts.Count == 0
            ? "Your transponder database matches the published copy."
            : string.Join(", ", parts) + " from tle.oscarwatch.org.";
    }
}

public partial class MergeAdditionItem : ObservableObject
{
    public MergeAdditionItem(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected = true;
}

public partial class MergeConflictItem : ObservableObject
{
    public MergeConflictItem(
        string key,
        string satelliteName,
        string modeType,
        string localSummary,
        string remoteSummary)
    {
        Key = key;
        SatelliteName = satelliteName;
        ModeType = modeType;
        LocalSummary = localSummary;
        RemoteSummary = remoteSummary;
    }

    public string Key { get; }
    public string SatelliteName { get; }
    public string ModeType { get; }
    public string LocalSummary { get; }
    public string RemoteSummary { get; }
    public string Title => $"{SatelliteName} · {ModeType}";

    [ObservableProperty]
    private bool _useRemote;
}
