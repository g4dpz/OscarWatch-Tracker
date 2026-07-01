using OscarWatch.Recording;

namespace OscarWatch.Tests;

public sealed class RecordingDeviceNameFormatterTests
{
    [Fact]
    public void Format_ExtractsDeviceNameFromSystemPath()
    {
        // Pattern: "Headset (@System32\drivers\...\(Device Name))"
        var formatted = RecordingDeviceNameFormatter.Format(
            "Headset (@System32\\drivers\\bthhefenum.sys,#2;%1 Hands-Free%0 ;(Soundcore P3))");
        
        Assert.Equal("Soundcore P3", formatted);
    }

    [Fact]
    public void Format_ExtractsDeviceFromComplexSystemPath()
    {
        var formatted = RecordingDeviceNameFormatter.Format(
            "Microphone (@System32\\drivers\\bthhefenum.sys,#2;%1 Hands-Free%0 ;(WF-C700N))");
        
        Assert.Equal("WF-C700N", formatted);
    }

    [Fact]
    public void Format_FixesMalformedParentheses()
    {
        // Missing closing paren
        var formatted = RecordingDeviceNameFormatter.Format("IC-910 (Main) (USB Audio CODEC");
        
        Assert.Equal("IC-910 (Main) (USB Audio CODEC)", formatted);
    }

    [Fact]
    public void Format_FixesMultipleMissingParentheses()
    {
        var formatted = RecordingDeviceNameFormatter.Format("Device (Type) (Subtype");
        
        Assert.Equal("Device (Type) (Subtype)", formatted);
    }

    [Fact]
    public void Format_PreservesWellFormedNames()
    {
        var formatted = RecordingDeviceNameFormatter.Format("IC-910 (Main) (USB Audio CODEC)");
        
        Assert.Equal("IC-910 (Main) (USB Audio CODEC)", formatted);
    }

    [Fact]
    public void Format_TrimsWhitespace()
    {
        var formatted = RecordingDeviceNameFormatter.Format("  Microphone  ");
        
        Assert.Equal("Microphone", formatted);
    }

    [Fact]
    public void Format_HandlesEmptyOrWhitespaceInput()
    {
        Assert.Empty(RecordingDeviceNameFormatter.Format(""));
        Assert.NotEmpty(RecordingDeviceNameFormatter.Format("   "));
        // Whitespace-only strings trim down but the formatter still returns them
    }

    [Fact]
    public void Format_PreservesSimpleDeviceNames()
    {
        var names = new[]
        {
            "Primary Sound Capture Driver",
            "Microphone (Realtek HD Audio)",
            "Line In (USB Audio CODEC)"
        };

        foreach (var name in names)
        {
            var formatted = RecordingDeviceNameFormatter.Format(name);
            Assert.Equal(name, formatted);
        }
    }

    [Fact]
    public void Format_NormalizesWhitespaceBeforeClosingParen()
    {
        // "CODEC " with space before ) should become "CODEC)"
        var formatted = RecordingDeviceNameFormatter.Format("IC-910 (Main) (USB Audio CODEC ");
        
        Assert.Equal("IC-910 (Main) (USB Audio CODEC)", formatted);
    }

    [Fact]
    public void Format_CollapsesDuplicateSpaces()
    {
        var formatted = RecordingDeviceNameFormatter.Format("IC-910  (Main)  (USB)");
        
        Assert.Equal("IC-910 (Main) (USB)", formatted);
    }

    [Fact]
    public void Format_RemovesSpacesAfterOpeningParen()
    {
        var formatted = RecordingDeviceNameFormatter.Format("Device ( USB )");
        
        Assert.Equal("Device (USB)", formatted);
    }
}
