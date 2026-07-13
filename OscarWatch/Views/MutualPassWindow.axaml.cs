using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class MutualPassWindow : Window
{
    private static readonly Size StationsSize = new(480, 340);
    private static readonly Size CriteriaSize = new(480, 300);
    private static readonly Size WhenHoursSize = new(480, 380);
    private static readonly Size WhenRangeSize = new(500, 500);
    private static readonly Size ResultsSize = new(920, 580);

    private MutualPassViewModel? _viewModel;

    public MutualPassWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MutualPassViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyLayoutForStep(_viewModel);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (e.PropertyName is nameof(MutualPassViewModel.WizardStep)
            or nameof(MutualPassViewModel.TimeWindowModeIndex))
        {
            ApplyLayoutForStep(_viewModel);
        }
    }

    private void ApplyLayoutForStep(MutualPassViewModel vm)
    {
        if (vm.IsResultsStep)
        {
            MinWidth = 720;
            MinHeight = 440;
            Width = ResultsSize.Width;
            Height = ResultsSize.Height;
            return;
        }

        MinWidth = 440;
        MinHeight = 280;

        var size = vm.WizardStep switch
        {
            0 => StationsSize,
            1 => CriteriaSize,
            2 when vm.UseDateRange => WhenRangeSize,
            _ => WhenHoursSize
        };

        Width = size.Width;
        Height = size.Height;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnPassesPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(PassesGrid).Properties.IsRightButtonPressed)
            return;

        for (var node = e.Source as Visual; node is not null; node = node.GetVisualParent() as Visual)
        {
            if (node is DataGridRow row && row.DataContext is MutualPassRow passRow)
            {
                PassesGrid.SelectedItem = passRow;
                break;
            }
        }
    }

    private async void OnCopyPassClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MutualPassViewModel vm)
            return;

        if (PassesGrid.SelectedItem is not MutualPassRow row)
            return;

        var text = vm.FormatCopyText(row);
        if (string.IsNullOrEmpty(text))
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    private void OnOpenVisualizerClick(object? sender, RoutedEventArgs e) =>
        OpenVisualizerForSelectedPass();

    private void OnPassesDoubleTapped(object? sender, TappedEventArgs e) =>
        OpenVisualizerForSelectedPass();

    private void OpenVisualizerForSelectedPass()
    {
        if (DataContext is not MutualPassViewModel vm)
            return;

        if (PassesGrid.SelectedItem is not MutualPassRow row)
            return;

        var visualizerVm = vm.CreateVisualizerViewModel(row);
        if (visualizerVm is null)
            return;

        new MutualPassVisualizerWindow
        {
            DataContext = visualizerVm
        }.Show(this);
    }
}
