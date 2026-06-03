using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Services;
using OscarWatch.Localization;

namespace OscarWatch.ViewModels;

public partial class SatelliteDatabaseMergeViewModel : ViewModelBase
{
    private readonly ILocalizationService _l;
    private readonly SatelliteDatabaseMergePresentation _presentation;

    public SatelliteDatabaseMergeViewModel(
        SatelliteDatabaseMergePlan plan,
        ILocalizationService localization,
        SatelliteDatabaseMergePresentation presentation = SatelliteDatabaseMergePresentation.RemoteUpdate)
    {
        Plan = plan;
        _l = localization;
        _presentation = presentation;
        WindowTitle = presentation == SatelliteDatabaseMergePresentation.FileImport
            ? _l.Get("DbMerge.Title.Import")
            : _l.Get("DbMerge.Title.Remote");
        SummaryText = BuildSummary(plan, presentation);
        IntroHintText = presentation == SatelliteDatabaseMergePresentation.FileImport
            ? _l.Get("DbMerge.Intro.Import")
            : _l.Get("DbMerge.Intro.Remote");
        ConflictRemoteLabel = presentation == SatelliteDatabaseMergePresentation.FileImport
            ? _l.Get("DbMerge.UseImported")
            : _l.Get("DbMerge.UsePublished");
        ConflictRemoteSummaryPrefix = presentation == SatelliteDatabaseMergePresentation.FileImport
            ? _l.Get("DbMerge.Prefix.Imported")
            : _l.Get("DbMerge.Prefix.Published");

        foreach (var item in plan.NewSatellites)
        {
            var label = item.Entry.Modes.Count == 1
                ? _l.Get("DbMerge.Item.SatelliteOneMode", item.Entry.Name)
                : _l.Get("DbMerge.Item.SatelliteManyModes", item.Entry.Name, item.Entry.Modes.Count);
            NewSatellites.Add(new MergeAdditionItem(item.Key, label));
        }

        foreach (var item in plan.NewModes)
        {
            NewModes.Add(new MergeAdditionItem(
                item.Key,
                _l.Get("DbMerge.Item.ModeLine", item.SatelliteName, item.Mode.Type)));
        }

        foreach (var item in plan.Conflicts)
        {
            Conflicts.Add(new MergeConflictItem(
                item.Key,
                item.SatelliteName,
                item.ModeType,
                SatelliteDatabaseMerger.DescribeMode(item.LocalMode),
                SatelliteDatabaseMerger.DescribeMode(item.RemoteMode),
                ConflictRemoteSummaryPrefix,
                ConflictRemoteLabel,
                _l.Get("DbMerge.YoursFormat", SatelliteDatabaseMerger.DescribeMode(item.LocalMode))));
        }
    }

    public SatelliteDatabaseMergePlan Plan { get; }

    public string WindowTitle { get; }

    public string SummaryText { get; }

    public string IntroHintText { get; }

    public string ConflictRemoteLabel { get; }

    public string ConflictRemoteSummaryPrefix { get; }

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

    private string BuildSummary(SatelliteDatabaseMergePlan plan, SatelliteDatabaseMergePresentation presentation)
    {
        var parts = new List<string>();
        if (plan.NewSatellites.Count > 0)
        {
            parts.Add(plan.NewSatellites.Count == 1
                ? _l.Get("DbMerge.Summary.OneNewSatellite", plan.NewSatellites.Count)
                : _l.Get("DbMerge.Summary.ManyNewSatellites", plan.NewSatellites.Count));
        }

        if (plan.NewModes.Count > 0)
        {
            parts.Add(plan.NewModes.Count == 1
                ? _l.Get("DbMerge.Summary.OneNewMode", plan.NewModes.Count)
                : _l.Get("DbMerge.Summary.ManyNewModes", plan.NewModes.Count));
        }

        if (plan.Conflicts.Count > 0)
        {
            parts.Add(plan.Conflicts.Count == 1
                ? _l.Get("DbMerge.Summary.OneConflict", plan.Conflicts.Count)
                : _l.Get("DbMerge.Summary.ManyConflicts", plan.Conflicts.Count));
        }

        var source = presentation == SatelliteDatabaseMergePresentation.FileImport
            ? _l.Get("DbMerge.Source.Import")
            : _l.Get("DbMerge.Source.Remote");
        return parts.Count == 0
            ? _l.Get("DbMerge.Summary.Matches", source)
            : string.Join(", ", parts) + _l.Get("DbMerge.Summary.FromSuffix", source);
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
        string remoteSummary,
        string remoteSummaryPrefix,
        string useRemoteLabel,
        string yoursSummaryLine)
    {
        Key = key;
        SatelliteName = satelliteName;
        ModeType = modeType;
        LocalSummary = localSummary;
        RemoteSummary = remoteSummary;
        YoursSummaryLine = yoursSummaryLine;
        RemoteSummaryLine = $"{remoteSummaryPrefix}: {remoteSummary}";
        UseRemoteLabel = useRemoteLabel;
    }

    public string Key { get; }
    public string SatelliteName { get; }
    public string ModeType { get; }
    public string LocalSummary { get; }
    public string RemoteSummary { get; }
    public string YoursSummaryLine { get; }
    public string RemoteSummaryLine { get; }
    public string UseRemoteLabel { get; }
    public string Title => $"{SatelliteName} · {ModeType}";

    [ObservableProperty]
    private bool _useRemote;
}
