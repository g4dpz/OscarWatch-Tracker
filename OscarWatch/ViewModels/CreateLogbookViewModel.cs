using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Localization;

namespace OscarWatch.ViewModels;

public partial class CreateLogbookViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _l;
    private bool _suppressFieldCoercion;

    public CreateLogbookViewModel(ISettingsService settings, ILocalizationService localization)
    {
        _settings = settings;
        _l = localization;
        Reset();
    }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _myCallsign = "";

    [ObservableProperty]
    private string _myGridSquare = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = "";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridValidationIsValid))]
    [NotifyPropertyChangedFor(nameof(GridValidationIsInvalid))]
    private bool? _gridIsValid;

    public bool GridValidationIsValid => GridIsValid == true;

    public bool GridValidationIsInvalid => GridIsValid == false;

    public void Reset(QsoLogbook? template = null)
    {
        _suppressFieldCoercion = true;
        Name = "";
        var gs = _settings.Current.GroundStation;
        MyCallsign = template?.MyCallsign ?? "";
        MyGridSquare = !string.IsNullOrWhiteSpace(template?.MyGridSquare)
            ? template.MyGridSquare
            : gs.GridSquare;
        ErrorText = "";
        GridIsValid = MaidenheadLocator.GetLiveValidationState(MyGridSquare);
        _suppressFieldCoercion = false;
    }

    partial void OnMyGridSquareChanged(string value)
    {
        CoerceGrid(value);
        GridIsValid = MaidenheadLocator.GetLiveValidationState(MyGridSquare);
    }

    partial void OnMyCallsignChanged(string value) => CoerceCallsign(value);

    public bool TryConfirm([NotNullWhen(true)] out QsoLogbookCreateRequest? request)
    {
        request = null;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorText = _l.Get("Logbook.Error.NameRequired");
            return false;
        }

        if (!MaidenheadLocator.TryValidateGrids(MyGridSquare, out var grid, out var error, out var invalidSegment))
        {
            ErrorText = error switch
            {
                GridValidationError.TooManyGrids =>
                    _l.Get("Logbook.Error.GridTooMany", MaidenheadLocator.MaxGridCount),
                GridValidationError.InvalidSegment =>
                    _l.Get("Logbook.Error.GridInvalidSegment", invalidSegment ?? ""),
                _ => ""
            };
            return false;
        }

        request = new QsoLogbookCreateRequest
        {
            Name = Name.Trim(),
            MyCallsign = MyCallsign,
            MyGridSquare = grid
        };
        return true;
    }

    private void CoerceCallsign(string value)
    {
        if (_suppressFieldCoercion)
            return;

        var normalized = MaidenheadLocator.NormalizeCallsign(value);
        if (string.Equals(normalized, value, StringComparison.Ordinal))
            return;

        _suppressFieldCoercion = true;
        MyCallsign = normalized;
        _suppressFieldCoercion = false;
    }

    private void CoerceGrid(string value)
    {
        if (_suppressFieldCoercion)
            return;

        var normalized = MaidenheadLocator.UppercaseGridEntry(value);
        if (string.Equals(normalized, value, StringComparison.Ordinal))
            return;

        _suppressFieldCoercion = true;
        MyGridSquare = normalized;
        _suppressFieldCoercion = false;
    }
}
