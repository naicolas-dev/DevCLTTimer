using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using DevCLT.Core.Interfaces;
using DevCLT.Core.Models;

namespace DevCLT.WindowsApp.ViewModels;

public class HistoryViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private readonly IClock _clock;

    private int _selectedTab; // 0=Week, 1=Month
    private string _periodLabel = "";
    private int _weekOffset;
    private int _monthOffset;
    private bool _isEmpty;

    public ObservableCollection<DaySummary> DaySummaries { get; } = new();

    public int SelectedTab { get => _selectedTab; set { SetField(ref _selectedTab, value); _ = LoadData(); } }
    public string PeriodLabel { get => _periodLabel; set => SetField(ref _periodLabel, value); }
    public bool IsEmpty { get => _isEmpty; set => SetField(ref _isEmpty, value); }

    // Week totals
    private string _totalWork = "";
    private string _totalBreak = "";
    private string _totalOvertime = "";
    public string TotalWork { get => _totalWork; set => SetField(ref _totalWork, value); }
    public string TotalBreak { get => _totalBreak; set => SetField(ref _totalBreak, value); }
    public string TotalOvertime { get => _totalOvertime; set => SetField(ref _totalOvertime, value); }

    public ICommand PreviousPeriodCommand { get; }
    public ICommand NextPeriodCommand { get; }
    public ICommand GoBackCommand { get; }

    public event Action? BackRequested;

    public HistoryViewModel(IRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
        PreviousPeriodCommand = new RelayCommand(() => { AdjustOffset(-1); _ = LoadData(); });
        NextPeriodCommand = new RelayCommand(() => { AdjustOffset(1); _ = LoadData(); });
        GoBackCommand = new RelayCommand(() => BackRequested?.Invoke());
    }

    public async Task LoadData()
    {
        DateTime from, to;

        if (SelectedTab == 0) // Week
        {
            var today = DateTime.Today.AddDays(7 * _weekOffset);
            from = GetIsoWeekStart(today);
            to = from.AddDays(7);
            PeriodLabel = $"{from:dd/MM} — {to.AddDays(-1):dd/MM/yyyy}";
        }
        else // Month
        {
            var refDate = DateTime.Today.AddMonths(_monthOffset);
            from = new DateTime(refDate.Year, refDate.Month, 1);
            to = from.AddMonths(1);
            PeriodLabel = from.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("pt-BR"));
        }

        var summaries = await _repository.GetDaySummariesAsync(
            from.ToUniversalTime(), to.ToUniversalTime());

        DaySummaries.Clear();
        foreach (var s in summaries)
            DaySummaries.Add(s);

        IsEmpty = DaySummaries.Count == 0;

        // compute totals
        var tw = summaries.Sum(s => s.TotalWorkSeconds);
        var tb = summaries.Sum(s => s.TotalBreakSeconds);
        var tot = summaries.Sum(s => s.TotalOvertimeSeconds);
        TotalWork = TimeSpan.FromSeconds(tw).ToString(@"hh\:mm");
        TotalBreak = TimeSpan.FromSeconds(tb).ToString(@"hh\:mm");
        TotalOvertime = TimeSpan.FromSeconds(tot).ToString(@"hh\:mm");
    }

    private void AdjustOffset(int delta)
    {
        if (SelectedTab == 0)
            _weekOffset += delta;
        else
            _monthOffset += delta;
    }

    private static DateTime GetIsoWeekStart(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        // ISO: Monday = 0
        var daysToMonday = (dayOfWeek == 0) ? 6 : dayOfWeek - 1;
        return date.Date.AddDays(-daysToMonday);
    }
}
