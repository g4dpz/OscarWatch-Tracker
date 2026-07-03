using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Localization;

namespace OscarWatch.ViewModels;

public partial class LogbookSettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _l;
    private readonly IQsoLogbookRepository _repository;
    private readonly ICloudlogLookupService _lookup;
    private readonly ICloudlogQsoUploadService _upload;
    private long _logbookId;

    public LogbookSettingsViewModel(
        ISettingsService settings,
        ILocalizationService localization,
        IQsoLogbookRepository repository,
        ICloudlogLookupService lookup,
        ICloudlogQsoUploadService upload)
    {
        _settings = settings;
        _l = localization;
        _repository = repository;
        _lookup = lookup;
        _upload = upload;
    }

    public ObservableCollection<CloudlogStationProfile> StationProfiles { get; } = [];

    [ObservableProperty]
    private string _logbookName = "";

    [ObservableProperty]
    private string _cloudlogUrlSummary = "";

    [ObservableProperty]
    private bool _cloudlogConfigured;

    [ObservableProperty]
    private bool _cloudlogAutoUpload;

    [ObservableProperty]
    private CloudlogStationProfile? _selectedStationProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusText = "";

    [ObservableProperty]
    private bool _isLoadingProfiles;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    public bool CanSaveStationProfile =>
        !CloudlogAutoUpload || SelectedStationProfile is not null;

    public void LoadForLogbook(QsoLogbook logbook)
    {
        _logbookId = logbook.Id;
        LogbookName = logbook.Name;
        CloudlogAutoUpload = logbook.CloudlogAutoUpload;
        ErrorText = "";
        StatusText = "";

        var cloudlog = _settings.Current.Cloudlog;
        CloudlogConfigured = _lookup.CanUploadQsos(cloudlog);
        CloudlogUrlSummary = CloudlogConfigured
            ? CloudlogUrlHelper.NormalizeBaseUrl(cloudlog.BaseUrl)
            : _l.Get("Logbook.Settings.Cloudlog.NotConfigured");

        SelectedStationProfile = null;
        StationProfiles.Clear();
    }

    public async Task LoadStationProfilesAsync()
    {
        if (IsLoadingProfiles)
            return;

        IsLoadingProfiles = true;
        ErrorText = "";
        StatusText = "";

        try
        {
            var result = await _lookup.FetchStationProfilesAsync(_settings.Current.Cloudlog).ConfigureAwait(true);
            StationProfiles.Clear();
            if (!result.Ok)
            {
                ErrorText = result.ErrorMessage ?? _l.Get("Logbook.Settings.Cloudlog.LoadProfilesFailed");
                return;
            }

            foreach (var profile in result.Profiles)
                StationProfiles.Add(profile);

            var logbook = await _repository.GetLogbookByIdAsync(_logbookId).ConfigureAwait(true);
            if (logbook?.CloudlogStationProfileId is int savedId)
                SelectedStationProfile = StationProfiles.FirstOrDefault(p => p.StationId == savedId);

            if (SelectedStationProfile is null && StationProfiles.Count > 0)
                SelectedStationProfile = StationProfiles.FirstOrDefault(p => p.IsActive) ?? StationProfiles[0];

            StatusText = StationProfiles.Count == 0
                ? _l.Get("Logbook.Settings.Cloudlog.NoProfiles")
                : _l.Get("Logbook.Settings.Cloudlog.ProfilesLoaded", StationProfiles.Count);
        }
        finally
        {
            IsLoadingProfiles = false;
        }
    }

    [RelayCommand]
    private Task RefreshStationProfilesAsync() => LoadStationProfilesAsync();

    [RelayCommand]
    private async Task RetryFailedUploadsAsync()
    {
        ErrorText = "";
        await _upload.ResetFailedUploadsForRetryAsync(_logbookId).ConfigureAwait(true);
        StatusText = _l.Get("Logbook.Settings.Cloudlog.RetryStarted");
    }

    public bool TrySave([NotNullWhen(true)] out QsoLogbookCloudlogSettingsRequest? request)
    {
        request = null;
        ErrorText = "";

        if (CloudlogAutoUpload && !CloudlogConfigured)
        {
            ErrorText = _l.Get("Logbook.Settings.Cloudlog.ConfigureIntegrationsFirst");
            return false;
        }

        if (CloudlogAutoUpload && SelectedStationProfile is null)
        {
            ErrorText = _l.Get("Logbook.Settings.Cloudlog.StationRequired");
            return false;
        }

        request = new QsoLogbookCloudlogSettingsRequest
        {
            Id = _logbookId,
            CloudlogAutoUpload = CloudlogAutoUpload,
            CloudlogStationProfileId = SelectedStationProfile?.StationId
        };
        return true;
    }

    partial void OnCloudlogAutoUploadChanged(bool value) => OnPropertyChanged(nameof(CanSaveStationProfile));

    partial void OnSelectedStationProfileChanged(CloudlogStationProfile? value) =>
        OnPropertyChanged(nameof(CanSaveStationProfile));
}
