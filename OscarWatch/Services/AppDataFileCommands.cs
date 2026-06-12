using Avalonia.Controls;
using Avalonia.Platform.Storage;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.Views;

namespace OscarWatch.Services;

public static class AppDataFileCommands
{
    private static FilePickerFileType JsonFileType(ILocalizationService l) =>
        new(l.Get("DbEditor.FileType.Json"))
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json"]
        };

    public static async Task<string?> ExportSettingsAsync(
        Window owner,
        ISettingsService settings,
        ILocalizationService l)
    {
        var storage = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (storage is null)
            return l.Get("File.Export.Failed", "Storage unavailable.");

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = l.Get("File.Export.Settings.Title"),
            SuggestedFileName = "settings.json",
            DefaultExtension = "json",
            FileTypeChoices = [JsonFileType(l)]
        }).ConfigureAwait(true);

        if (file is null)
            return null;

        try
        {
            var json = settings.SerializeCurrent();
            await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json).ConfigureAwait(true);
            return l.Get("File.Export.Settings.Success");
        }
        catch (Exception ex)
        {
            return l.Get("File.Export.Failed", ex.Message);
        }
    }

    public static async Task<(bool Applied, string? Status)> ImportSettingsAsync(
        Window owner,
        ISettingsService settings,
        ILocalizationService l)
    {
        var storage = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (storage is null)
            return (false, l.Get("File.Import.Failed", "Storage unavailable."));

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = l.Get("File.Import.Settings.Title"),
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType(l)]
        }).ConfigureAwait(true);

        var file = files.FirstOrDefault();
        if (file is null)
            return (false, null);

        try
        {
            await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync().ConfigureAwait(true);

            if (!SettingsService.TryParse(json, out var imported, out var parseError))
                return (false, l.Get("File.Import.Settings.Invalid", parseError ?? "Unknown error"));

            if (!await SimpleConfirmDialog.ShowAsync(
                    owner,
                    l.Get("File.Import.Settings.ConfirmTitle"),
                    l.Get("File.Import.Settings.ConfirmMessage"))
                .ConfigureAwait(true))
            {
                return (false, null);
            }

            await settings.ReplaceAndSaveAsync(imported).ConfigureAwait(true);
            return (true, l.Get("File.Import.Settings.Success"));
        }
        catch (Exception ex)
        {
            return (false, l.Get("File.Import.Failed", ex.Message));
        }
    }

    public static async Task<string?> ExportTransponderDatabaseAsync(
        Window owner,
        ISatelliteDatabaseSyncService syncService,
        ILocalizationService l)
    {
        var storage = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (storage is null)
            return l.Get("File.Export.Failed", "Storage unavailable.");

        var entries = syncService.LoadLocalEntriesForMerge();
        var validationError = SatelliteDatabaseFile.ValidateEntries(entries);
        if (validationError is not null)
            return l.Get("DbEditor.Status.InvalidDatabase", validationError);

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = l.Get("File.Export.TransponderDatabase.Title"),
            SuggestedFileName = "satellite_database.json",
            DefaultExtension = "json",
            FileTypeChoices = [JsonFileType(l)]
        }).ConfigureAwait(true);

        if (file is null)
            return null;

        try
        {
            var json = SatelliteDatabaseFile.SerializeEntries(entries);
            await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json).ConfigureAwait(true);
            return l.Get("DbEditor.Status.Exported", entries.Count);
        }
        catch (Exception ex)
        {
            return l.Get("File.Export.Failed", ex.Message);
        }
    }

    public static async Task<(bool Applied, string? Status)> ImportTransponderDatabaseAsync(
        Window owner,
        ISatelliteDatabaseSyncService syncService,
        ILocalizationService l)
    {
        var storage = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (storage is null)
            return (false, l.Get("File.Import.Failed", "Storage unavailable."));

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = l.Get("File.Import.TransponderDatabase.Title"),
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType(l)]
        }).ConfigureAwait(true);

        var file = files.FirstOrDefault();
        if (file is null)
            return (false, null);

        try
        {
            await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync().ConfigureAwait(true);
            var imported = SatelliteDatabaseFile.ParseJson(json);
            var validationError = SatelliteDatabaseFile.ValidateEntries(imported);
            if (validationError is not null)
                return (false, l.Get("DbEditor.Status.InvalidDatabase", validationError));

            var local = syncService.LoadLocalEntriesForMerge();
            var plan = SatelliteDatabaseMerger.BuildPlan(local, imported);
            if (!plan.HasChanges)
                return (false, l.Get("DbEditor.Status.ImportNoChanges"));

            var result = await TransponderDatabaseMergeDialog.TryMergeApplyAsync(
                owner,
                plan,
                local,
                SatelliteDatabaseMergePresentation.FileImport).ConfigureAwait(true);

            if (result is null)
                return (false, l.Get("DbEditor.Status.ImportCancelled"));

            syncService.SaveMergedEntries(result.Merged);
            return (true, l.Get("DbEditor.Status.ImportApplied"));
        }
        catch (Exception ex)
        {
            return (false, l.Get("File.Import.Failed", ex.Message));
        }
    }
}
