using System.Globalization;

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

    // ── Display properties ──

    /// <summary>"Seg, 16/02"</summary>
    public string FormattedDate
    {
        get
        {
            if (DateTime.TryParse(DateLocal, out var dt))
            {
                var culture = CultureInfo.GetCultureInfo("pt-BR");
                var dow = culture.DateTimeFormat.GetAbbreviatedDayName(dt.DayOfWeek);
                dow = char.ToUpper(dow[0]) + dow[1..];
                return $"{dow}, {dt:dd/MM}";
            }
            return DateLocal;
        }
    }

    public bool HasOvertime => TotalOvertimeSeconds > 0;

    public string FormattedWork => FormatOrDash(TotalWorkSeconds);
    public string FormattedBreak => FormatOrDash(TotalBreakSeconds);
    public string FormattedOvertime => FormatOrDash(TotalOvertimeSeconds);

    private static string FormatOrDash(int totalSeconds)
        => totalSeconds <= 0 ? "—" : TimeSpan.FromSeconds(totalSeconds).ToString(@"hh\:mm");
}
