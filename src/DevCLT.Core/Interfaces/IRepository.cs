using DevCLT.Core.Models;

namespace DevCLT.Core.Interfaces;

public interface IRepository
{
    // Session
    Task<long> CreateSessionAsync(Session session);
    Task UpdateSessionAsync(Session session);
    Task<Session?> GetActiveSessionAsync();
    Task<Session?> GetSessionByIdAsync(long id);

    // Segments
    Task<long> CreateSegmentAsync(Segment segment);
    Task UpdateSegmentAsync(Segment segment);
    Task<List<Segment>> GetSegmentsBySessionIdAsync(long sessionId);

    // Settings
    Task SaveSettingsAsync(AppSettings settings);
    Task<AppSettings> LoadSettingsAsync();

    // Reports
    Task<List<DaySummary>> GetDaySummariesAsync(DateTime fromUtc, DateTime toUtc);
}
