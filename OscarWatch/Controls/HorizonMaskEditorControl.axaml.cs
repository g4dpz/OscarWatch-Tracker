using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OscarWatch.Core.Models;

namespace OscarWatch.Controls;

public partial class HorizonMaskEditorControl : UserControl
{
    public static readonly StyledProperty<ObservableCollection<HorizonMaskPoint>?> PointsProperty =
        AvaloniaProperty.Register<HorizonMaskEditorControl, ObservableCollection<HorizonMaskPoint>?>(nameof(Points));

    public HorizonMaskEditorControl()
    {
        InitializeComponent();
    }

    public ObservableCollection<HorizonMaskPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e) =>
        Points?.Clear();
}
