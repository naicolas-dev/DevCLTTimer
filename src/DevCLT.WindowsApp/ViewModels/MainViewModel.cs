using System.Windows.Input;
using DevCLT.Core.Engine;
using DevCLT.Core.Interfaces;
using DevCLT.Core.Models;

namespace DevCLT.WindowsApp.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly TimerEngine _engine;
    private readonly IRepository _repository;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    private ViewModelBase? _currentView;
    private bool _showRecoveryCard;
    private Session? _recoverySession;

    public ViewModelBase? CurrentView { get => _currentView; set => SetField(ref _currentView, value); }
    public bool ShowRecoveryCard { get => _showRecoveryCard; set => SetField(ref _showRecoveryCard, value); }

    public SetupViewModel SetupVM { get; }
    public TimerViewModel TimerVM { get; }
    public HistoryViewModel HistoryVM { get; }

    public ICommand ResumeRecoveryCommand { get; }
    public ICommand DiscardRecoveryCommand { get; }
    public ICommand ShowHistoryCommand { get; }

    // For tray menu
    public SessionState CurrentEngineState => _engine.State;

    public MainViewModel(TimerEngine engine, IRepository repository, INotifier notifier, IClock clock)
    {
        _engine = engine;
        _repository = repository;
        _notifier = notifier;
        _clock = clock;

        SetupVM = new SetupViewModel(repository);
        TimerVM = new TimerViewModel(engine, repository, notifier, clock);
        HistoryVM = new HistoryViewModel(repository, clock);

        SetupVM.StartRequested += OnStartRequested;
        TimerVM.SessionEnded += OnSessionEnded;
        HistoryVM.BackRequested += () => ShowSetup();

        ResumeRecoveryCommand = new RelayCommand(async () => await ResumeRecovery());
        DiscardRecoveryCommand = new RelayCommand(async () => await DiscardRecovery());
        ShowHistoryCommand = new RelayCommand(async () => await GoToHistory());

        CurrentView = SetupVM;
    }

    public async Task InitializeAsync()
    {
        await SetupVM.LoadSettings();

        // Check for unfinished session (recovery)
        _recoverySession = await _repository.GetActiveSessionAsync();
        if (_recoverySession != null)
        {
            ShowRecoveryCard = true;
        }
    }

    private async void OnStartRequested(int workMin, int breakMin, int overtimeNotifyMin)
    {
        CurrentView = TimerVM;
        await TimerVM.StartSession(workMin, breakMin, overtimeNotifyMin);
    }

    private void OnSessionEnded()
    {
        ShowSetup();
    }

    private void ShowSetup()
    {
        CurrentView = SetupVM;
    }

    private async Task GoToHistory()
    {
        CurrentView = HistoryVM;
        await HistoryVM.LoadData();
    }

    private async Task ResumeRecovery()
    {
        if (_recoverySession == null) return;
        ShowRecoveryCard = false;

        // Restore engine state
        var segments = await _repository.GetSegmentsBySessionIdAsync(_recoverySession.Id);
        var totalWork = TimeSpan.Zero;
        var totalBreak = TimeSpan.Zero;
        DateTime? currentSegStart = null;

        foreach (var seg in segments)
        {
            if (seg.EndUtc.HasValue)
            {
                var dur = TimeSpan.FromSeconds(seg.DurationSeconds ?? (int)(seg.EndUtc.Value - seg.StartUtc).TotalSeconds);
                if (seg.Type == SegmentType.Work) totalWork += dur;
                else if (seg.Type == SegmentType.Break) totalBreak += dur;
            }
            else
            {
                // open segment — this is where we were
                currentSegStart = seg.StartUtc;
            }
        }

        _engine.Configure(_recoverySession.TargetWorkMinutes, _recoverySession.TargetBreakMinutes,
            (await _repository.LoadSettingsAsync()).OvertimeNotifyIntervalMinutes);

        var state = _recoverySession.ActiveState ?? SessionState.Working;
        DateTime? overtimeStart = state == SessionState.Overtime ? currentSegStart : null;

        _engine.Restore(state, totalWork, totalBreak, currentSegStart, overtimeStart, _recoverySession.Id);

        CurrentView = TimerVM;
        TimerVM.StartTicker();
    }

    private async Task DiscardRecovery()
    {
        ShowRecoveryCard = false;
        if (_recoverySession != null)
        {
            _recoverySession.EndedAtUtc = _clock.UtcNow;
            _recoverySession.ActiveState = null;
            await _repository.UpdateSessionAsync(_recoverySession);
            _recoverySession = null;
        }
    }
}
