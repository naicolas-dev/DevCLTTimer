namespace DevCLT.Core.Models;

public enum SessionState
{
    Idle,
    Working,
    Break,
    BreakEndedWaitingUser,
    WorkCompleted,
    Overtime
}
