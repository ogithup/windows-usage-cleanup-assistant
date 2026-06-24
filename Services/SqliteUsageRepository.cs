using System.IO;
using Microsoft.Data.Sqlite;
using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public sealed class SqliteUsageRepository : IUsageRepository
{
    private readonly string _connectionString;
    private readonly object _syncRoot = new();

    public SqliteUsageRepository(string databasePath)
    {
        var directoryPath = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
        }.ToString();
    }

    public void Initialize()
    {
        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS UsageRecords (
                    RecordKey TEXT PRIMARY KEY,
                    ProcessName TEXT NOT NULL,
                    ExecutablePath TEXT NOT NULL DEFAULT '',
                    FirstSeenUtc TEXT NOT NULL,
                    LastSeenUtc TEXT NOT NULL,
                    TotalObservedMinutes REAL NOT NULL DEFAULT 0,
                    LaunchCount INTEGER NOT NULL DEFAULT 0
                );
                """;
            command.ExecuteNonQuery();
        }
    }

    public void RecordObservation(string processName, string executablePath, DateTime observedAtUtc, double observedMinutes, bool incrementLaunchCount)
    {
        var normalizedPath = executablePath.Trim();
        var normalizedName = processName.Trim();
        var recordKey = BuildRecordKey(normalizedName, normalizedPath);
        var observedAt = observedAtUtc.ToString("O");

        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO UsageRecords (
                    RecordKey,
                    ProcessName,
                    ExecutablePath,
                    FirstSeenUtc,
                    LastSeenUtc,
                    TotalObservedMinutes,
                    LaunchCount
                )
                VALUES (
                    $recordKey,
                    $processName,
                    $executablePath,
                    $observedAtUtc,
                    $observedAtUtc,
                    $observedMinutes,
                    $launchCount
                )
                ON CONFLICT(RecordKey) DO UPDATE SET
                    ProcessName = excluded.ProcessName,
                    ExecutablePath = CASE
                        WHEN UsageRecords.ExecutablePath = '' AND excluded.ExecutablePath <> '' THEN excluded.ExecutablePath
                        ELSE UsageRecords.ExecutablePath
                    END,
                    LastSeenUtc = excluded.LastSeenUtc,
                    TotalObservedMinutes = UsageRecords.TotalObservedMinutes + excluded.TotalObservedMinutes,
                    LaunchCount = UsageRecords.LaunchCount + excluded.LaunchCount;
                """;
            command.Parameters.AddWithValue("$recordKey", recordKey);
            command.Parameters.AddWithValue("$processName", normalizedName);
            command.Parameters.AddWithValue("$executablePath", normalizedPath);
            command.Parameters.AddWithValue("$observedAtUtc", observedAt);
            command.Parameters.AddWithValue("$observedMinutes", observedMinutes);
            command.Parameters.AddWithValue("$launchCount", incrementLaunchCount ? 1 : 0);
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<UsageRecord> GetUsageRecords()
    {
        var records = new List<UsageRecord>();

        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    ProcessName,
                    ExecutablePath,
                    FirstSeenUtc,
                    LastSeenUtc,
                    TotalObservedMinutes,
                    LaunchCount
                FROM UsageRecords
                ORDER BY LastSeenUtc DESC, ProcessName ASC;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                records.Add(new UsageRecord
                {
                    ProcessName = reader.GetString(0),
                    ExecutablePath = reader.GetString(1),
                    FirstSeenUtc = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    LastSeenUtc = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    TotalObservedMinutes = reader.GetDouble(4),
                    LaunchCount = reader.GetInt32(5),
                });
            }
        }

        return records;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static string BuildRecordKey(string processName, string executablePath)
    {
        return string.IsNullOrWhiteSpace(executablePath)
            ? $"name:{processName.ToLowerInvariant()}"
            : $"path:{executablePath.ToLowerInvariant()}";
    }
}
