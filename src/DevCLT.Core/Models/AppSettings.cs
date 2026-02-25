namespace DevCLT.Core.Models;

public class AppSettings
{
    public int WorkDurationMinutes { get; set; } = 480; // 8h
    public int BreakDurationMinutes { get; set; } = 60;  // 1h
    public int OvertimeNotifyIntervalMinutes { get; set; } = 30; // 0 = never
    public bool IsDarkTheme { get; set; }
    public bool HasCompletedOnboarding { get; set; }

    // Hotkeys
    public bool HotkeysEnabled { get; set; } = true;
    public string HotkeyJornada { get; set; } = "Ctrl+Alt+I";
    public string HotkeyPausa { get; set; } = "Ctrl+Alt+P";
    public string HotkeyOvertime { get; set; } = "Ctrl+Alt+X";
}
