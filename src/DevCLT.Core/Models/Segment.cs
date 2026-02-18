namespace DevCLT.Core.Models;

public class Segment
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public SegmentType Type { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public int? DurationSeconds { get; set; }
}
