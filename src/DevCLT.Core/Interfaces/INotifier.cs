namespace DevCLT.Core.Interfaces;

public interface INotifier
{
    void NotifyBreakEnded();
    void NotifyWorkCompleted();
    void NotifyOvertime(TimeSpan elapsed);
    void NotifyOvertimeMuted(TimeSpan elapsed);
}
