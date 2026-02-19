using System.Windows.Input;
using System.Windows.Threading;
using DevCLT.Core.Engine;
using DevCLT.Core.Interfaces;
using DevCLT.Core.Models;

namespace DevCLT.WindowsApp.ViewModels;

public class TimerViewModel : ViewModelBase
{
    private readonly TimerEngine _engine;
    private readonly IRepository _repository;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly DispatcherTimer _ticker;

    private string _displayTime = "00:00:00";
    private string _stateLabel = "";
    private bool _isWarning;
    private double _progress;
    private bool _showBreakEndedModal;
    private bool _showWorkCompletedModal;
    private bool _showEndDayConfirm;
    private bool _showStartBreakConfirm;
    private bool _showEndBreakEarlyConfirm;
    private bool _isBreakBackground;
    private string _badgeStyle = "Working";

    // active segment tracking
    private long _currentSegmentId;

    public string DisplayTime { get => _displayTime; set => SetField(ref _displayTime, value); }
    public string StateLabel { get => _stateLabel; set => SetField(ref _stateLabel, value); }
    public bool IsWarning { get => _isWarning; set => SetField(ref _isWarning, value); }
    public double Progress { get => _progress; set => SetField(ref _progress, value); }
    public bool ShowBreakEndedModal { get => _showBreakEndedModal; set => SetField(ref _showBreakEndedModal, value); }
    public bool ShowWorkCompletedModal { get => _showWorkCompletedModal; set => SetField(ref _showWorkCompletedModal, value); }
    public bool ShowEndDayConfirm { get => _showEndDayConfirm; set => SetField(ref _showEndDayConfirm, value); }
    public bool ShowStartBreakConfirm { get => _showStartBreakConfirm; set => SetField(ref _showStartBreakConfirm, value); }
    public bool ShowEndBreakEarlyConfirm { get => _showEndBreakEarlyConfirm; set => SetField(ref _showEndBreakEarlyConfirm, value); }
    public bool IsBreakBackground { get => _isBreakBackground; set => SetField(ref _isBreakBackground, value); }
    public string BadgeStyle { get => _badgeStyle; set => SetField(ref _badgeStyle, value); }
    
    private string _trayStatusText = "Dev CLT Timer";
    public string TrayStatusText { get => _trayStatusText; set => SetField(ref _trayStatusText, value); }

    public SessionState CurrentState => _engine.State;

    // Commands
    public ICommand StartBreakCommand { get; }
    public ICommand ResumeWorkCommand { get; }
    public ICommand StartOvertimeCommand { get; }
    public ICommand StopOvertimeCommand { get; }
    public ICommand EndDayCommand { get; }
    public ICommand ShowEndDayConfirmCommand { get; }
    public ICommand CancelEndDayCommand { get; }
    public ICommand ConfirmEndDayCommand { get; }
    public ICommand ShowStartBreakConfirmCommand { get; }
    public ICommand CancelStartBreakCommand { get; }
    public ICommand ConfirmStartBreakCommand { get; }
    public ICommand EndBreakEarlyCommand { get; }
    public ICommand ShowEndBreakEarlyConfirmCommand { get; }
    public ICommand CancelEndBreakEarlyCommand { get; }

    public event Action? SessionEnded;

    public TimerViewModel(TimerEngine engine, IRepository repository, INotifier notifier, IClock clock)
    {
        _engine = engine;
        _repository = repository;
        _notifier = notifier;
        _clock = clock;

        _engine.BreakEnded += OnBreakEnded;
        _engine.WorkCompleted += OnWorkCompleted;
        _engine.OvertimeNotification += OnOvertimeNotify;
        _engine.StateChanged += OnStateChanged;

        StartBreakCommand = new RelayCommand(async () => await DoStartBreak(),
            () => _engine.State == SessionState.Working);
        ResumeWorkCommand = new RelayCommand(async () => await DoResumeWork(),
            () => _engine.State == SessionState.BreakEndedWaitingUser);
        StartOvertimeCommand = new RelayCommand(async () => await DoStartOvertime(),
            () => _engine.State == SessionState.WorkCompleted);
        StopOvertimeCommand = new RelayCommand(async () => await DoStopOvertime(),
            () => _engine.State == SessionState.Overtime);
        EndDayCommand = new RelayCommand(async () => await DoEndDay(),
            () => _engine.State == SessionState.WorkCompleted);
        ShowEndDayConfirmCommand = new RelayCommand(() => ShowEndDayConfirm = true,
            () => _engine.State == SessionState.Working);
        CancelEndDayCommand = new RelayCommand(() => ShowEndDayConfirm = false);
        ConfirmEndDayCommand = new RelayCommand(async () => await DoEndDayEarly());
        ShowStartBreakConfirmCommand = new RelayCommand(() => ShowStartBreakConfirm = true,
            () => _engine.State == SessionState.Working);
        CancelStartBreakCommand = new RelayCommand(() => ShowStartBreakConfirm = false);
        ConfirmStartBreakCommand = new RelayCommand(async () => await DoConfirmStartBreak());
        EndBreakEarlyCommand = new RelayCommand(async () => await DoEndBreakEarly(),
            () => _engine.State == SessionState.Break);
        ShowEndBreakEarlyConfirmCommand = new RelayCommand(() => ShowEndBreakEarlyConfirm = true,
            () => _engine.State == SessionState.Break);
        CancelEndBreakEarlyCommand = new RelayCommand(() => ShowEndBreakEarlyConfirm = false);

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _ticker.Tick += (_, _) => Tick();
    }

    public async Task StartSession(int workMin, int breakMin, int overtimeNotifyMin)
    {
        _engine.Configure(workMin, breakMin, overtimeNotifyMin);
        _engine.StartWork();

        // persist session
        var session = new Session
        {
            DateLocal = DateTime.Now.ToString("yyyy-MM-dd"),
            TargetWorkMinutes = workMin,
            TargetBreakMinutes = breakMin,
            CreatedAtUtc = _clock.UtcNow,
            ActiveState = SessionState.Working
        };
        session.Id = await _repository.CreateSessionAsync(session);
        _engine.CurrentSessionId = session.Id;

        // persist work segment
        var seg = new Segment
        {
            SessionId = session.Id,
            Type = SegmentType.Work,
            StartUtc = _clock.UtcNow
        };
        seg.Id = await _repository.CreateSegmentAsync(seg);
        _currentSegmentId = seg.Id;

        _ticker.Start();
        UpdateDisplay();
    }

    public void StartTicker()
    {
        _ticker.Start();
        UpdateDisplay();
    }

    private void Tick()
    {
        _engine.Tick();
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var state = _engine.State;
        IsBreakBackground = state == SessionState.Break;

        switch (state)
        {
            case SessionState.Working:
                var remaining = _engine.RemainingWork;
                if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                DisplayTime = remaining.ToString(@"hh\:mm\:ss");
                StateLabel = "Trabalho";
                Progress = 1.0 - (remaining / _engine.TargetWorkDuration);
                BadgeStyle = _engine.IsWarning ? "Warning" : "Working";
                break;

            case SessionState.Break:
                var breakRem = _engine.RemainingBreak;
                if (breakRem < TimeSpan.Zero) breakRem = TimeSpan.Zero;
                DisplayTime = breakRem.ToString(@"hh\:mm\:ss");
                StateLabel = "Pausa";
                Progress = 1.0 - (breakRem / _engine.TargetBreakDuration);
                BadgeStyle = _engine.IsWarning ? "Warning" : "Neutral";
                break;

            case SessionState.BreakEndedWaitingUser:
                DisplayTime = "00:00:00";
                StateLabel = "Pausa concluída";
                Progress = 1.0;
                BadgeStyle = "Neutral";
                break;

            case SessionState.WorkCompleted:
                DisplayTime = "00:00:00";
                StateLabel = "Jornada concluída";
                Progress = 1.0;
                BadgeStyle = "Working";
                break;

            case SessionState.Overtime:
                var ot = _engine.ElapsedOvertime;
                DisplayTime = ot.ToString(@"hh\:mm\:ss");
                StateLabel = "Hora Extra";
                Progress = 0; // no ring for overtime
                BadgeStyle = "Overtime";
                break;
        }

        IsWarning = _engine.IsWarning;
        IsWarning = _engine.IsWarning;
        
        // Update Tray Text
        if (state == SessionState.Idle)
            TrayStatusText = "Dev CLT Timer (Parado)";
        else
            TrayStatusText = $"{StateLabel}: {DisplayTime}";

        OnPropertyChanged(nameof(CurrentState));
    }

    // ── Engine event handlers ──

    private void OnBreakEnded()
    {
        _notifier.NotifyBreakEnded();
        ShowBreakEndedModal = true;
        _ = FinalizeCurrentSegment();
    }

    private void OnWorkCompleted()
    {
        _notifier.NotifyWorkCompleted();
        ShowWorkCompletedModal = true;
        _ = FinalizeCurrentSegment();
    }

    private void OnOvertimeNotify(TimeSpan elapsed)
    {
        _notifier.NotifyOvertime(elapsed);
    }

    private void OnStateChanged()
    {
        _ = UpdateSessionState();
    }

    // ── Command implementations ──

    private async Task DoStartBreak()
    {
        await FinalizeCurrentSegment();
        _engine.StartBreak();

        var seg = new Segment
        {
            SessionId = _engine.CurrentSessionId,
            Type = SegmentType.Break,
            StartUtc = _clock.UtcNow
        };
        seg.Id = await _repository.CreateSegmentAsync(seg);
        _currentSegmentId = seg.Id;
    }

    private async Task DoConfirmStartBreak()
    {
        ShowStartBreakConfirm = false;
        await DoStartBreak();
    }

    private async Task DoEndBreakEarly()
    {
        ShowEndBreakEarlyConfirm = false;
        await FinalizeCurrentSegment();
        _engine.EndBreakEarly();

        var seg = new Segment
        {
            SessionId = _engine.CurrentSessionId,
            Type = SegmentType.Work,
            StartUtc = _clock.UtcNow
        };
        seg.Id = await _repository.CreateSegmentAsync(seg);
        _currentSegmentId = seg.Id;
    }

    private async Task DoResumeWork()
    {
        ShowBreakEndedModal = false;
        _engine.ResumeWork();

        var seg = new Segment
        {
            SessionId = _engine.CurrentSessionId,
            Type = SegmentType.Work,
            StartUtc = _clock.UtcNow
        };
        seg.Id = await _repository.CreateSegmentAsync(seg);
        _currentSegmentId = seg.Id;
    }

    private async Task DoStartOvertime()
    {
        ShowWorkCompletedModal = false;
        _engine.StartOvertime();

        var seg = new Segment
        {
            SessionId = _engine.CurrentSessionId,
            Type = SegmentType.Overtime,
            StartUtc = _clock.UtcNow
        };
        seg.Id = await _repository.CreateSegmentAsync(seg);
        _currentSegmentId = seg.Id;
    }

    private async Task DoStopOvertime()
    {
        await FinalizeCurrentSegment();
        _engine.StopOvertime();
        await FinalizeSession();
        _ticker.Stop();
        SessionEnded?.Invoke();
    }

    private async Task DoEndDay()
    {
        ShowWorkCompletedModal = false;
        _engine.EndDay();
        await FinalizeSession();
        _ticker.Stop();
        SessionEnded?.Invoke();
    }

    private async Task DoEndDayEarly()
    {
        ShowEndDayConfirm = false;
        await FinalizeCurrentSegment();
        _engine.EndDayEarly();
        await FinalizeSession();
        _ticker.Stop();
        SessionEnded?.Invoke();
    }

    // ── Persistence helpers ──

    private async Task FinalizeCurrentSegment()
    {
        if (_currentSegmentId <= 0) return;
        var now = _clock.UtcNow;
        var segments = await _repository.GetSegmentsBySessionIdAsync(_engine.CurrentSessionId);
        var seg = segments.FirstOrDefault(s => s.Id == _currentSegmentId);
        if (seg != null && seg.EndUtc == null)
        {
            seg.EndUtc = now;
            seg.DurationSeconds = (int)(now - seg.StartUtc).TotalSeconds;
            await _repository.UpdateSegmentAsync(seg);
        }
    }

    private async Task FinalizeSession()
    {
        var session = await _repository.GetSessionByIdAsync(_engine.CurrentSessionId);
        if (session != null)
        {
            session.EndedAtUtc = _clock.UtcNow;
            session.ActiveState = null;
            await _repository.UpdateSessionAsync(session);
        }
    }

    private async Task UpdateSessionState()
    {
        var session = await _repository.GetSessionByIdAsync(_engine.CurrentSessionId);
        if (session != null && session.EndedAtUtc == null)
        {
            session.ActiveState = _engine.State;
            await _repository.UpdateSessionAsync(session);
        }
    }

    public async Task DiscardSession()
    {
        _ticker.Stop();
        var session = await _repository.GetSessionByIdAsync(_engine.CurrentSessionId);
        if (session != null)
        {
            session.EndedAtUtc = _clock.UtcNow;
            session.ActiveState = null;
            await _repository.UpdateSessionAsync(session);
        }
    }
}
