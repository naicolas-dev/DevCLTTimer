namespace DevCLT.Core.Models;

public class Session
{
    public long Id { get; set; }
    public string DateLocal { get; set; } = string.Empty; // YYYY-MM-DD
    public int TargetWorkMinutes { get; set; }
    public int TargetBreakMinutes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>
    /// Current state persisted for recovery. Null if session ended normally.
    /// </summary>
    public SessionState? ActiveState { get; set; }
}
