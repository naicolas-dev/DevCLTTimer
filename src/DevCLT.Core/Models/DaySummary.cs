namespace DevCLT.Core.Models;

public class DaySummary
{
    public string DateLocal { get; set; } = string.Empty;
    public int TotalWorkSeconds { get; set; }
    public int TotalBreakSeconds { get; set; }
    public int TotalOvertimeSeconds { get; set; }

    public TimeSpan WorkTime => TimeSpan.FromSeconds(TotalWorkSeconds);
    public TimeSpan BreakTime => TimeSpan.FromSeconds(TotalBreakSeconds);
    public TimeSpan OvertimeTime => TimeSpan.FromSeconds(TotalOvertimeSeconds);
}
