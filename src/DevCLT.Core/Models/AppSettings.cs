namespace DevCLT.Core.Models;

public class AppSettings
{
    public int WorkDurationMinutes { get; set; } = 480; // 8h
    public int BreakDurationMinutes { get; set; } = 60;  // 1h
    public int OvertimeNotifyIntervalMinutes { get; set; } = 30; // 0 = never
}
