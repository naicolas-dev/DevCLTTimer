using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using DevCLT.Core.Interfaces;
using DevCLT.Core.Models;

namespace DevCLT.WindowsApp.ViewModels;

public class HistoryViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private readonly IClock _clock;
    private readonly ICsvExportService _csvExport;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private int _selectedTab; // 0=Week, 1=Month, 2=Year
    private string _periodLabel = "";
    private int _weekOffset;
    private int _monthOffset;
    private int _yearOffset;
    private bool _isEmpty;

    public ObservableCollection<DaySummary> DaySummaries { get; } = new();

    public int SelectedTab { get => _selectedTab; set { SetField(ref _selectedTab, value); OnPropertyChanged(nameof(GoToTodayLabel)); _ = LoadData(); } }
    public string PeriodLabel { get => _periodLabel; set => SetField(ref _periodLabel, value); }
    public bool IsEmpty { get => _isEmpty; set => SetField(ref _isEmpty, value); }
    public string GoToTodayLabel => _selectedTab switch { 0 => "Esta semana", 1 => "Este mês", _ => "Este ano" };

    // Period totals
    private string _totalWork = "";
    private string _totalBreak = "";
    private string _totalOvertime = "";
    private bool _hasTotalOvertime;
    public string TotalWork { get => _totalWork; set => SetField(ref _totalWork, value); }
    public string TotalBreak { get => _totalBreak; set => SetField(ref _totalBreak, value); }
    public string TotalOvertime { get => _totalOvertime; set => SetField(ref _totalOvertime, value); }
    public bool HasTotalOvertime { get => _hasTotalOvertime; set => SetField(ref _hasTotalOvertime, value); }

    public ICommand PreviousPeriodCommand { get; }
    public ICommand NextPeriodCommand { get; }
    public ICommand GoToTodayCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public event Action? BackRequested;

    public HistoryViewModel(IRepository repository, IClock clock, ICsvExportService csvExport)
    {
        _repository = repository;
        _clock = clock;
        _csvExport = csvExport;
        PreviousPeriodCommand = new RelayCommand(() => { AdjustOffset(-1); _ = LoadData(); });
        NextPeriodCommand = new RelayCommand(() => { AdjustOffset(1); _ = LoadData(); });
        GoToTodayCommand = new RelayCommand(() => { _weekOffset = 0; _monthOffset = 0; _yearOffset = 0; _ = LoadData(); });
        GoBackCommand = new RelayCommand(() => BackRequested?.Invoke());
        ExportCsvCommand = new RelayCommand(async () => await ExportCsvAsync());
    }

    public async Task LoadData()
    {
        DateTime from, to;

        if (SelectedTab == 0) // Week
        {
            var today = DateTime.Today.AddDays(7 * _weekOffset);
            from = GetIsoWeekStart(today);
            to = from.AddDays(7);

            // "16–22 fev 2026"
            var fromDay = from.Day.ToString();
            var toDay = (to.AddDays(-1)).Day.ToString();
            var monthName = from.ToString("MMM", PtBr).TrimEnd('.');
            var year = from.Year;
            PeriodLabel = $"{fromDay}–{toDay} {monthName} {year}";
        }
        else if (SelectedTab == 1) // Month
        {
            var refDate = DateTime.Today.AddMonths(_monthOffset);
            from = new DateTime(refDate.Year, refDate.Month, 1);
            to = from.AddMonths(1);
            PeriodLabel = from.ToString("MMMM yyyy", PtBr);
        }
        else // Year
        {
            var refYear = DateTime.Today.Year + _yearOffset;
            from = new DateTime(refYear, 1, 1);
            to = new DateTime(refYear + 1, 1, 1);
            PeriodLabel = refYear.ToString();
        }

        var summaries = await _repository.GetDaySummariesAsync(
            from.ToUniversalTime(), to.ToUniversalTime());

        DaySummaries.Clear();

        if (SelectedTab == 2) // Year: aggregate by month
        {
            var grouped = summaries
                .Where(s => DateTime.TryParse(s.DateLocal, out _))
                .GroupBy(s => DateTime.Parse(s.DateLocal).Month)
                .OrderBy(g => g.Key);

            foreach (var g in grouped)
            {
                var monthName = new DateTime(from.Year, g.Key, 1).ToString("MMMM", PtBr);
                monthName = char.ToUpper(monthName[0]) + monthName[1..];
                DaySummaries.Add(new DaySummary
                {
                    DateLocal = monthName,
                    TotalWorkSeconds = g.Sum(s => s.TotalWorkSeconds),
                    TotalBreakSeconds = g.Sum(s => s.TotalBreakSeconds),
                    TotalOvertimeSeconds = g.Sum(s => s.TotalOvertimeSeconds),
                });
            }
        }
        else
        {
            foreach (var s in summaries)
                DaySummaries.Add(s);
        }

        IsEmpty = DaySummaries.Count == 0;

        // compute totals
        var tw = summaries.Sum(s => s.TotalWorkSeconds);
        var tb = summaries.Sum(s => s.TotalBreakSeconds);
        var tot = summaries.Sum(s => s.TotalOvertimeSeconds);
        TotalWork = FormatOrDash(tw);
        TotalBreak = FormatOrDash(tb);
        TotalOvertime = FormatOrDash(tot);
        HasTotalOvertime = tot > 0;
    }

    private void AdjustOffset(int delta)
    {
        if (SelectedTab == 0)
            _weekOffset += delta;
        else if (SelectedTab == 1)
            _monthOffset += delta;
        else
            _yearOffset += delta;
    }

    private static string FormatOrDash(int totalSeconds)
    {
        if (totalSeconds <= 0) return "—";
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}";
    }

    private static DateTime GetIsoWeekStart(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        // ISO: Monday = 0
        var daysToMonday = (dayOfWeek == 0) ? 6 : dayOfWeek - 1;
        return date.Date.AddDays(-daysToMonday);
    }

    private async Task ExportCsvAsync()
    {
        if (DaySummaries.Count == 0) return;

        var tabName = SelectedTab switch { 0 => "semana", 1 => "mes", _ => "ano" };
        var safePeriod = Regex.Replace(PeriodLabel, @"[^\w\d]+", "-").Trim('-').ToLowerInvariant();
        var fileName = $"devclt_{tabName}_{safePeriod}.csv";

        await _csvExport.ExportAsync(DaySummaries, fileName);
    }
}
