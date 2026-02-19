using DevCLT.Core.Interfaces;
using DevCLT.Core.Models;

namespace DevCLT.Core.Engine;

/// <summary>
/// The core state machine and timer engine. Platform-agnostic.
/// Uses DateTime.UtcNow (via IClock) for resilience to sleep/lag.
/// The UI layer calls Tick() on a timer (e.g. DispatcherTimer every 500ms).
/// </summary>
public class TimerEngine
{
    private readonly IClock _clock;

    public SessionState State { get; private set; } = SessionState.Idle;

    // configuration
    public TimeSpan TargetWorkDuration { get; private set; }
    public TimeSpan TargetBreakDuration { get; private set; }
    public int OvertimeNotifyIntervalMinutes { get; private set; }

    // timing anchors (UTC)
    public DateTime? WorkStartUtc { get; private set; }
    public DateTime? BreakStartUtc { get; private set; }
    public DateTime? OvertimeStartUtc { get; private set; }

    // accumulated work/break before current segment
    private TimeSpan _accumulatedWork;
    private TimeSpan _accumulatedBreak;

    // current segment start for the active work or break period
    public DateTime? CurrentSegmentStartUtc { get; private set; }

    // overtime notification tracking
    private DateTime? _lastOvertimeNotifyUtc;

    // session tracking
    public long CurrentSessionId { get; set; }

    // events for the UI/notifier layer
    public event Action? BreakEnded;
    public event Action? WorkCompleted;
    public event Action<TimeSpan>? OvertimeNotification;
    public event Action? StateChanged;

    public TimerEngine(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Remaining work time (countdown). Negative means overtime-eligible.
    /// </summary>
    public TimeSpan RemainingWork
    {
        get
        {
            if (State == SessionState.Idle) return TargetWorkDuration;
            var totalWorked = _accumulatedWork;
            if (State == SessionState.Working && CurrentSegmentStartUtc.HasValue)
                totalWorked += _clock.UtcNow - CurrentSegmentStartUtc.Value;
            return TargetWorkDuration - totalWorked;
        }
    }

    /// <summary>
    /// Remaining break time (countdown).
    /// </summary>
    public TimeSpan RemainingBreak
    {
        get
        {
            if (State != SessionState.Break) return TargetBreakDuration - _accumulatedBreak;
            var totalBreak = _accumulatedBreak;
            if (CurrentSegmentStartUtc.HasValue)
                totalBreak += _clock.UtcNow - CurrentSegmentStartUtc.Value;
            return TargetBreakDuration - totalBreak;
        }
    }

    /// <summary>
    /// Elapsed overtime (count-up).
    /// </summary>
    public TimeSpan ElapsedOvertime
    {
        get
        {
            if (State != SessionState.Overtime || !OvertimeStartUtc.HasValue) return TimeSpan.Zero;
            return _clock.UtcNow - OvertimeStartUtc.Value;
        }
    }

    /// <summary>
    /// Total work done so far, including current segment if working.
    /// </summary>
    public TimeSpan TotalWorkDone
    {
        get
        {
            var total = _accumulatedWork;
            if (State == SessionState.Working && CurrentSegmentStartUtc.HasValue)
                total += _clock.UtcNow - CurrentSegmentStartUtc.Value;
            return total;
        }
    }

    /// <summary>
    /// Whether warning state should be shown (near end of work or break).
    /// </summary>
    public bool IsWarning
    {
        get
        {
            return State switch
            {
                SessionState.Working => RemainingWork <= TimeSpan.FromMinutes(10) && RemainingWork > TimeSpan.Zero,
                SessionState.Break => RemainingBreak <= TimeSpan.FromMinutes(2) && RemainingBreak > TimeSpan.Zero,
                _ => false,
            };
        }
    }

    // ── Commands ──

    public void Configure(int workMinutes, int breakMinutes, int overtimeNotifyMinutes)
    {
        TargetWorkDuration = TimeSpan.FromMinutes(workMinutes);
        TargetBreakDuration = TimeSpan.FromMinutes(breakMinutes);
        OvertimeNotifyIntervalMinutes = overtimeNotifyMinutes;
    }

    public bool StartWork()
    {
        if (State != SessionState.Idle) return false;

        var now = _clock.UtcNow;
        WorkStartUtc = now;
        CurrentSegmentStartUtc = now;
        _accumulatedWork = TimeSpan.Zero;
        _accumulatedBreak = TimeSpan.Zero;
        State = SessionState.Working;
        StateChanged?.Invoke();
        return true;
    }

    public bool StartBreak()
    {
        if (State != SessionState.Working) return false;

        var now = _clock.UtcNow;
        // accumulate work done in this segment
        if (CurrentSegmentStartUtc.HasValue)
            _accumulatedWork += now - CurrentSegmentStartUtc.Value;

        BreakStartUtc = now;
        CurrentSegmentStartUtc = now;
        State = SessionState.Break;
        StateChanged?.Invoke();
        return true;
    }

    public bool EndBreakEarly()
    {
        if (State != SessionState.Break) return false;

        var now = _clock.UtcNow;
        if (CurrentSegmentStartUtc.HasValue)
            _accumulatedBreak += now - CurrentSegmentStartUtc.Value;

        CurrentSegmentStartUtc = now;
        State = SessionState.Working;
        StateChanged?.Invoke();
        return true;
    }

    public bool ResumeWork()
    {
        if (State != SessionState.BreakEndedWaitingUser) return false;

        var now = _clock.UtcNow;
        CurrentSegmentStartUtc = now;
        State = SessionState.Working;
        StateChanged?.Invoke();
        return true;
    }

    public bool StartOvertime()
    {
        if (State != SessionState.WorkCompleted) return false;

        var now = _clock.UtcNow;
        OvertimeStartUtc = now;
        CurrentSegmentStartUtc = now;
        _lastOvertimeNotifyUtc = now;
        State = SessionState.Overtime;
        StateChanged?.Invoke();
        return true;
    }

    public bool StopOvertime()
    {
        if (State != SessionState.Overtime) return false;

        State = SessionState.Idle;
        Reset();
        StateChanged?.Invoke();
        return true;
    }

    public bool EndDay()
    {
        if (State != SessionState.WorkCompleted) return false;

        State = SessionState.Idle;
        Reset();
        StateChanged?.Invoke();
        return true;
    }

    public bool EndDayEarly()
    {
        if (State != SessionState.Working) return false;

        var now = _clock.UtcNow;
        if (CurrentSegmentStartUtc.HasValue)
            _accumulatedWork += now - CurrentSegmentStartUtc.Value;

        State = SessionState.Idle;
        Reset();
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Called by the UI timer (e.g. every 500ms). Checks for state transitions.
    /// </summary>
    public void Tick()
    {
        switch (State)
        {
            case SessionState.Working:
                if (RemainingWork <= TimeSpan.Zero)
                {
                    // Finalize current work segment
                    if (CurrentSegmentStartUtc.HasValue)
                        _accumulatedWork += _clock.UtcNow - CurrentSegmentStartUtc.Value;
                    CurrentSegmentStartUtc = null;
                    State = SessionState.WorkCompleted;
                    WorkCompleted?.Invoke();
                    StateChanged?.Invoke();
                }
                break;

            case SessionState.Break:
                if (RemainingBreak <= TimeSpan.Zero)
                {
                    // Finalize current break segment
                    if (CurrentSegmentStartUtc.HasValue)
                        _accumulatedBreak += _clock.UtcNow - CurrentSegmentStartUtc.Value;
                    CurrentSegmentStartUtc = null;
                    State = SessionState.BreakEndedWaitingUser;
                    BreakEnded?.Invoke();
                    StateChanged?.Invoke();
                }
                break;

            case SessionState.Overtime:
                if (OvertimeNotifyIntervalMinutes > 0 && _lastOvertimeNotifyUtc.HasValue)
                {
                    var sinceLastNotify = _clock.UtcNow - _lastOvertimeNotifyUtc.Value;
                    if (sinceLastNotify >= TimeSpan.FromMinutes(OvertimeNotifyIntervalMinutes))
                    {
                        _lastOvertimeNotifyUtc = _clock.UtcNow;
                        OvertimeNotification?.Invoke(ElapsedOvertime);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Restore engine state for recovery (crash/reboot).
    /// </summary>
    public void Restore(SessionState state, TimeSpan accumulatedWork, TimeSpan accumulatedBreak,
        DateTime? currentSegmentStart, DateTime? overtimeStart, long sessionId)
    {
        State = state;
        _accumulatedWork = accumulatedWork;
        _accumulatedBreak = accumulatedBreak;
        CurrentSegmentStartUtc = currentSegmentStart;
        OvertimeStartUtc = overtimeStart;
        CurrentSessionId = sessionId;
        if (state == SessionState.Overtime)
            _lastOvertimeNotifyUtc = _clock.UtcNow;
        StateChanged?.Invoke();
    }

    private void Reset()
    {
        WorkStartUtc = null;
        BreakStartUtc = null;
        OvertimeStartUtc = null;
        CurrentSegmentStartUtc = null;
        _accumulatedWork = TimeSpan.Zero;
        _accumulatedBreak = TimeSpan.Zero;
        _lastOvertimeNotifyUtc = null;
        CurrentSessionId = 0;
    }
}
