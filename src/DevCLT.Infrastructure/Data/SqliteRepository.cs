using DevCLT.Core.Interfaces;
using DevCLT.Core.Models;
using Microsoft.Data.Sqlite;

namespace DevCLT.Infrastructure.Data;

public class SqliteRepository : IRepository
{
    private readonly string _connectionString;

    public SqliteRepository(IAppPaths paths)
    {
        _connectionString = $"Data Source={paths.DatabasePath}";
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    // ── Sessions ──

    public async Task<long> CreateSessionAsync(Session session)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO sessions (date_local, target_work_minutes, target_break_minutes, created_at_utc, active_state)
            VALUES (@date, @work, @brk, @created, @state);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@date", session.DateLocal);
        cmd.Parameters.AddWithValue("@work", session.TargetWorkMinutes);
        cmd.Parameters.AddWithValue("@brk", session.TargetBreakMinutes);
        cmd.Parameters.AddWithValue("@created", session.CreatedAtUtc.ToString("o"));
        cmd.Parameters.AddWithValue("@state", session.ActiveState?.ToString() ?? (object)DBNull.Value);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateSessionAsync(Session session)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE sessions SET
                ended_at_utc = @ended,
                active_state = @state
            WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", session.Id);
        cmd.Parameters.AddWithValue("@ended", session.EndedAtUtc?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@state", session.ActiveState?.ToString() ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Session?> GetActiveSessionAsync()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sessions WHERE active_state IS NOT NULL AND ended_at_utc IS NULL ORDER BY id DESC LIMIT 1;";
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return ReadSession(reader);
    }

    public async Task<Session?> GetSessionByIdAsync(long id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sessions WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return ReadSession(reader);
    }

    private static Session ReadSession(SqliteDataReader reader)
    {
        var session = new Session
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            DateLocal = reader.GetString(reader.GetOrdinal("date_local")),
            TargetWorkMinutes = reader.GetInt32(reader.GetOrdinal("target_work_minutes")),
            TargetBreakMinutes = reader.GetInt32(reader.GetOrdinal("target_break_minutes")),
            CreatedAtUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at_utc"))).ToUniversalTime(),
        };

        var endedOrd = reader.GetOrdinal("ended_at_utc");
        if (!reader.IsDBNull(endedOrd))
            session.EndedAtUtc = DateTime.Parse(reader.GetString(endedOrd)).ToUniversalTime();

        var stateOrd = reader.GetOrdinal("active_state");
        if (!reader.IsDBNull(stateOrd))
            session.ActiveState = Enum.Parse<SessionState>(reader.GetString(stateOrd));

        return session;
    }

    // ── Segments ──

    public async Task<long> CreateSegmentAsync(Segment segment)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO segments (session_id, type, start_utc, end_utc, duration_seconds)
            VALUES (@sid, @type, @start, @end, @dur);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@sid", segment.SessionId);
        cmd.Parameters.AddWithValue("@type", segment.Type.ToString());
        cmd.Parameters.AddWithValue("@start", segment.StartUtc.ToString("o"));
        cmd.Parameters.AddWithValue("@end", segment.EndUtc?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@dur", segment.DurationSeconds ?? (object)DBNull.Value);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateSegmentAsync(Segment segment)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE segments SET
                end_utc = @end,
                duration_seconds = @dur
            WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", segment.Id);
        cmd.Parameters.AddWithValue("@end", segment.EndUtc?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@dur", segment.DurationSeconds ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Segment>> GetSegmentsBySessionIdAsync(long sessionId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM segments WHERE session_id = @sid ORDER BY start_utc;";
        cmd.Parameters.AddWithValue("@sid", sessionId);
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<Segment>();
        while (await reader.ReadAsync())
        {
            var seg = new Segment
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                SessionId = reader.GetInt64(reader.GetOrdinal("session_id")),
                Type = Enum.Parse<SegmentType>(reader.GetString(reader.GetOrdinal("type"))),
                StartUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("start_utc"))).ToUniversalTime(),
            };
            var endOrd = reader.GetOrdinal("end_utc");
            if (!reader.IsDBNull(endOrd))
                seg.EndUtc = DateTime.Parse(reader.GetString(endOrd)).ToUniversalTime();
            var durOrd = reader.GetOrdinal("duration_seconds");
            if (!reader.IsDBNull(durOrd))
                seg.DurationSeconds = reader.GetInt32(durOrd);
            list.Add(seg);
        }
        return list;
    }

    // ── Settings ──

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        await UpsertSetting(conn, "WorkDurationMinutes", settings.WorkDurationMinutes.ToString());
        await UpsertSetting(conn, "BreakDurationMinutes", settings.BreakDurationMinutes.ToString());
        await UpsertSetting(conn, "OvertimeNotifyIntervalMinutes", settings.OvertimeNotifyIntervalMinutes.ToString());
        await UpsertSetting(conn, "IsDarkTheme", settings.IsDarkTheme.ToString());
        await UpsertSetting(conn, "HasCompletedOnboarding", settings.HasCompletedOnboarding.ToString());
        await UpsertSetting(conn, "HotkeysEnabled", settings.HotkeysEnabled.ToString());
        await UpsertSetting(conn, "HotkeyJornada", settings.HotkeyJornada);
        await UpsertSetting(conn, "HotkeyPausa", settings.HotkeyPausa);
        await UpsertSetting(conn, "HotkeyOvertime", settings.HotkeyOvertime);

        tx.Commit();
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        using var conn = Open();
        var settings = new AppSettings();

        var dict = new Dictionary<string, string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings;";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            dict[reader.GetString(0)] = reader.GetString(1);

        if (dict.TryGetValue("WorkDurationMinutes", out var w) && int.TryParse(w, out var wv))
            settings.WorkDurationMinutes = wv;
        if (dict.TryGetValue("BreakDurationMinutes", out var b) && int.TryParse(b, out var bv))
            settings.BreakDurationMinutes = bv;
        if (dict.TryGetValue("OvertimeNotifyIntervalMinutes", out var o) && int.TryParse(o, out var ov))
            settings.OvertimeNotifyIntervalMinutes = ov;
        if (dict.TryGetValue("IsDarkTheme", out var d) && bool.TryParse(d, out var dv))
            settings.IsDarkTheme = dv;
        if (dict.TryGetValue("HasCompletedOnboarding", out var onboarding) && bool.TryParse(onboarding, out var onboardingValue))
            settings.HasCompletedOnboarding = onboardingValue;
        if (dict.TryGetValue("HotkeysEnabled", out var he) && bool.TryParse(he, out var hev))
            settings.HotkeysEnabled = hev;
        if (dict.TryGetValue("HotkeyJornada", out var hj))
            settings.HotkeyJornada = hj;
        if (dict.TryGetValue("HotkeyPausa", out var hp))
            settings.HotkeyPausa = hp;
        if (dict.TryGetValue("HotkeyOvertime", out var ho))
            settings.HotkeyOvertime = ho;

        return settings;
    }

    private static async Task UpsertSetting(SqliteConnection conn, string key, string value)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO settings (key, value) VALUES (@k, @v) ON CONFLICT(key) DO UPDATE SET value = @v;";
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Reports ──

    public async Task<List<DaySummary>> GetDaySummariesAsync(DateTime fromUtc, DateTime toUtc)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT s.date_local,
                   COALESCE(SUM(CASE WHEN seg.type = 'Work' THEN seg.duration_seconds ELSE 0 END), 0) as work_sec,
                   COALESCE(SUM(CASE WHEN seg.type = 'Break' THEN seg.duration_seconds ELSE 0 END), 0) as break_sec,
                   COALESCE(SUM(CASE WHEN seg.type = 'Overtime' THEN seg.duration_seconds ELSE 0 END), 0) as ot_sec
            FROM sessions s
            JOIN segments seg ON seg.session_id = s.id
            WHERE s.created_at_utc >= @from AND s.created_at_utc < @to
              AND seg.duration_seconds IS NOT NULL
            GROUP BY s.date_local
            ORDER BY s.date_local DESC;";
        cmd.Parameters.AddWithValue("@from", fromUtc.ToString("o"));
        cmd.Parameters.AddWithValue("@to", toUtc.ToString("o"));

        var list = new List<DaySummary>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new DaySummary
            {
                DateLocal = reader.GetString(0),
                TotalWorkSeconds = reader.GetInt32(1),
                TotalBreakSeconds = reader.GetInt32(2),
                TotalOvertimeSeconds = reader.GetInt32(3),
            });
        }
        return list;
    }
}
