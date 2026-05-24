using OscarWatch.Core.Display;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class RecordingFileNameFormatTests
{
    [Fact]
    public void BuildFileName_uses_utc_stamp_and_lowercase_satellite_name()
    {
        var utc = new DateTime(2026, 5, 24, 14, 30, 0, DateTimeKind.Utc);
        var fileName = RecordingFileNameFormat.BuildFileName("SO-50", utc);
        Assert.Equal("so-50-26-05-24-14-30.wav", fileName);
    }

    [Fact]
    public void BuildFileName_sanitizes_invalid_characters()
    {
        var utc = new DateTime(2026, 1, 2, 3, 4, 0, DateTimeKind.Utc);
        var fileName = RecordingFileNameFormat.BuildFileName("AO 91/B", utc);
        Assert.Equal("ao-91b-26-01-02-03-04.wav", fileName);
    }

    [Fact]
    public void ResolveUniquePath_appends_suffix_when_file_exists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "oscarwatch-rec-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var utc = new DateTime(2026, 5, 24, 14, 30, 0, DateTimeKind.Utc);
            var first = RecordingFileNameFormat.ResolveUniquePath(dir, "ISS", utc);
            File.WriteAllText(first, "x");

            var second = RecordingFileNameFormat.ResolveUniquePath(dir, "ISS", utc);
            Assert.Equal(Path.Combine(dir, "iss-26-05-24-14-30-2.wav"), second);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
