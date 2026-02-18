using DevCLT.Core.Interfaces;
using Microsoft.Data.Sqlite;

namespace DevCLT.Infrastructure.Data;

public class DatabaseInitializer
{
    private readonly IAppPaths _paths;

    public DatabaseInitializer(IAppPaths paths)
    {
        _paths = paths;
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(_paths.DataDirectory);

        using var connection = new SqliteConnection($"Data Source={_paths.DatabasePath}");
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                date_local TEXT NOT NULL,
                target_work_minutes INTEGER NOT NULL,
                target_break_minutes INTEGER NOT NULL,
                created_at_utc TEXT NOT NULL,
                ended_at_utc TEXT,
                active_state TEXT
            );

            CREATE TABLE IF NOT EXISTS segments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL,
                type TEXT NOT NULL CHECK(type IN ('Work','Break','Overtime')),
                start_utc TEXT NOT NULL,
                end_utc TEXT,
                duration_seconds INTEGER,
                FOREIGN KEY (session_id) REFERENCES sessions(id)
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_segments_session ON segments(session_id);
            CREATE INDEX IF NOT EXISTS idx_sessions_date ON sessions(date_local);
        ";
        cmd.ExecuteNonQuery();
    }
}
