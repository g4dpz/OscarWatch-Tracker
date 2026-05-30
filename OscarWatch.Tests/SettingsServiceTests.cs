using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task SaveAsync_serializes_concurrent_writes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oscarwatch-settings-{Guid.NewGuid():N}.json");
        var service = new SettingsService(path);
        service.Current.GroundStation.DisplayName = "Home";

        try
        {
            var tasks = Enumerable.Range(0, 20)
                .Select(i =>
                {
                    service.Current.GroundStation.DisplayName = $"Station-{i}";
                    return service.SaveAsync();
                })
                .ToArray();

            await Task.WhenAll(tasks);

            Assert.True(File.Exists(path));
            var json = await File.ReadAllTextAsync(path);
            Assert.Contains("Station-19", json);
        }
        finally
        {
            DeleteIfExists(path);
            DeleteIfExists(path + ".tmp");
            DeleteIfExists(path + ".bak");
        }
    }

    [Fact]
    public void WriteAtomic_replaces_existing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oscarwatch-atomic-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{ \"old\": true }");
            SettingsService.WriteAtomic(path, "{ \"new\": true }");

            var json = File.ReadAllText(path);
            Assert.Contains("\"new\": true", json);
            Assert.DoesNotContain("\"old\": true", json);
        }
        finally
        {
            DeleteIfExists(path);
            DeleteIfExists(path + ".tmp");
            DeleteIfExists(path + ".bak");
        }
    }

    [Fact]
    public async Task RequestSave_reports_failure_when_destination_is_not_writable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oscarwatch-settings-{Guid.NewGuid():N}.json");
        Exception? reported = null;
        void Handler(Exception ex) => reported = ex;
        SettingsService.SaveFailed += Handler;

        try
        {
            var service = new SettingsService(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{}");
            MakeFileReadOnly(path);

            service.RequestSave();

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (reported is null && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            Assert.NotNull(reported);
        }
        finally
        {
            SettingsService.SaveFailed -= Handler;
            MakeFileWritable(path);
            DeleteIfExists(path);
            DeleteIfExists(path + ".tmp");
            DeleteIfExists(path + ".bak");
        }
    }

    private static void MakeFileReadOnly(string path)
    {
        if (OperatingSystem.IsWindows())
            File.SetAttributes(path, FileAttributes.ReadOnly);
        else
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }

    private static void MakeFileWritable(string path)
    {
        if (!File.Exists(path))
            return;

        if (OperatingSystem.IsWindows())
            File.SetAttributes(path, FileAttributes.Normal);
        else
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
