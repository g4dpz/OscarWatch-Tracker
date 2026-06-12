using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Validates mode change and CTCSS tone programming commands in satellite mode.
/// Validates: Requirements 6.1, 6.2, 6.3, 6.4, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6
/// </summary>
public class Ts2000ModeToneTests : Ts2000TestBase
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Mode Change Tests — Requirements 6.1, 6.2, 6.3, 6.4
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requirement 6.1: In satellite mode with main VFO selected, SetMode sends
    /// SA1010110; then the MD command.
    /// </summary>
    [Fact]
    public void SetMode_main_vfo_sends_SA1010110_then_MD()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Main);
        ClearCommandLog();

        Driver.SetMode("USB");

        AssertCommandSequence("SA1010110;", "MD2;");
    }

    /// <summary>
    /// Requirement 6.2: In satellite mode with sub VFO selected, SetMode sends
    /// SA1011110; then MD then SA1010110; to return to main-control.
    /// </summary>
    [Fact]
    public void SetMode_sub_vfo_sends_SA1011110_then_MD_then_SA1010110()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Sub);
        ClearCommandLog();

        Driver.SetMode("LSB");

        AssertCommandSequence("SA1011110;", "MD1;", "SA1010110;");
    }

    /// <summary>
    /// Requirement 6.3: No DC commands sent for mode changes in satellite mode.
    /// </summary>
    [Fact]
    public void SetMode_satellite_mode_sends_no_DC_commands()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Main);
        ClearCommandLog();

        Driver.SetMode("CW");

        AssertNoCommandStartingWith("DC");
    }

    /// <summary>
    /// Requirement 6.4: Unsupported mode string logs warning and sends no commands.
    /// </summary>
    [Fact]
    public void SetMode_unsupported_mode_sends_no_commands()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Main);
        ClearCommandLog();

        Driver.SetMode("INVALID_MODE");

        Assert.Empty(GetSentCommands());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Tone Programming Tests — Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requirement 7.1: SetToneHz(hz, squelchTone: false) on main VFO sends
    /// SA1010110; then TN{index};
    /// </summary>
    [Fact]
    public void SetToneHz_main_vfo_sends_SA1010110_then_TN()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Main);
        ClearCommandLog();

        Driver.SetToneHz(67.0, squelchTone: false);

        AssertCommandSequence("SA1010110;", "TN01;");
    }

    /// <summary>
    /// Requirement 7.2: SetToneHz(hz, squelchTone: false) on sub VFO sends
    /// SA1011110; then TN{index}; then SA1010110;
    /// </summary>
    [Fact]
    public void SetToneHz_sub_vfo_sends_SA1011110_then_TN_then_SA1010110()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Sub);
        ClearCommandLog();

        Driver.SetToneHz(67.0, squelchTone: false);

        AssertCommandSequence("SA1011110;", "TN01;", "SA1010110;");
    }

    /// <summary>
    /// Requirement 7.3: SetToneHz(hz, squelchTone: true) sends CN{index}; using same SA pattern.
    /// </summary>
    [Fact]
    public void SetToneHz_squelch_main_vfo_sends_SA1010110_then_CN()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Main);
        ClearCommandLog();

        Driver.SetToneHz(67.0, squelchTone: true);

        AssertCommandSequence("SA1010110;", "CN01;");
    }

    /// <summary>
    /// Requirement 7.4: SetToneOn(true) sends TO1; with SA bracketing for selected VFO.
    /// </summary>
    [Fact]
    public void SetToneOn_main_vfo_sends_SA1010110_then_TO1()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Main);
        ClearCommandLog();

        Driver.SetToneOn(true);

        AssertCommandSequence("SA1010110;", "TO1;");
    }

    /// <summary>
    /// Requirement 7.5: SetToneSquelchOn(true) sends CT1; with SA bracketing for selected VFO.
    /// </summary>
    [Fact]
    public void SetToneSquelchOn_main_vfo_sends_SA1010110_then_CT1()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Main);
        ClearCommandLog();

        Driver.SetToneSquelchOn(true);

        AssertCommandSequence("SA1010110;", "CT1;");
    }

    /// <summary>
    /// Requirement 7.6: Unsupported CTCSS frequency logs warning and sends no commands.
    /// </summary>
    [Fact]
    public void SetToneHz_unsupported_frequency_sends_no_commands()
    {
        EnterSatelliteMode();
        Driver.SelectVfo(RigVfo.Main);
        ClearCommandLog();

        Driver.SetToneHz(12345.0, squelchTone: false);

        Assert.Empty(GetSentCommands());
    }
}
