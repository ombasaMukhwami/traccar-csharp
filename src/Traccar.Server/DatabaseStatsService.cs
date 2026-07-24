using Microsoft.EntityFrameworkCore;
using Npgsql;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server;

/// <summary>
/// Reads PostgreSQL's own built-in stats views (pg_stat_database, pg_stat_user_tables,
/// pg_stat_activity) directly via ADO.NET rather than EF entities, since none of this is
/// application data. Shared by <see cref="Controllers.DatabaseController"/> (traccar-style route)
/// and <see cref="Controllers.AdministrativeDatabaseController"/> (the Blazor fleet-management
/// frontend's route) so the query logic lives in exactly one place.
/// </summary>
public static class DatabaseStatsService
{
    public static async Task<DatabaseStats> GetStatsAsync(TraccarDbContext db)
    {
        // OpenConnectionAsync/CloseConnectionAsync are ref-counted, so this nests safely
        // regardless of whatever connection state EF Core itself is holding.
        await db.Database.OpenConnectionAsync();
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            var stats = new DatabaseStats();

            await using (var cmd = new NpgsqlCommand(
                """
                SELECT pg_database_size(current_database()),
                       numbackends, xact_commit, xact_rollback, blks_read, blks_hit,
                       tup_inserted, tup_updated, tup_deleted
                FROM pg_stat_database
                WHERE datname = current_database()
                """, connection))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    stats.DatabaseSizeBytes = reader.GetInt64(0);
                    stats.ActiveConnections = reader.GetInt32(1);
                    stats.TransactionsCommitted = reader.GetInt64(2);
                    stats.TransactionsRolledBack = reader.GetInt64(3);
                    stats.BlocksRead = reader.GetInt64(4);
                    stats.BlocksHit = reader.GetInt64(5);
                    stats.TuplesInserted = reader.GetInt64(6);
                    stats.TuplesUpdated = reader.GetInt64(7);
                    stats.TuplesDeleted = reader.GetInt64(8);
                }
            }

            await using (var cmd = new NpgsqlCommand(
                """
                SELECT relname, n_live_tup, n_dead_tup, seq_scan, COALESCE(idx_scan, 0), pg_total_relation_size(relid)
                FROM pg_stat_user_tables
                ORDER BY pg_total_relation_size(relid) DESC
                """, connection))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    stats.Tables.Add(new TableStats
                    {
                        TableName = reader.GetString(0),
                        LiveRows = reader.GetInt64(1),
                        DeadRows = reader.GetInt64(2),
                        SequentialScans = reader.GetInt64(3),
                        IndexScans = reader.GetInt64(4),
                        TotalSizeBytes = reader.GetInt64(5),
                    });
                }
            }

            // Currently-open backend sessions against this database — client_addr is cast to
            // text since Npgsql's default `inet` mapping isn't worth the extra handling just to
            // display it.
            await using (var cmd = new NpgsqlCommand(
                """
                SELECT pid, usename, application_name, client_addr::text, state, query_start, query
                FROM pg_stat_activity
                WHERE datname = current_database()
                ORDER BY query_start DESC NULLS LAST
                """, connection))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    stats.Activity.Add(new PgActivity
                    {
                        Pid = reader.GetInt32(0),
                        Username = reader.IsDBNull(1) ? null : reader.GetString(1),
                        ApplicationName = reader.IsDBNull(2) ? null : reader.GetString(2),
                        ClientAddress = reader.IsDBNull(3) ? null : reader.GetString(3),
                        State = reader.IsDBNull(4) ? null : reader.GetString(4),
                        QueryStart = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                        Query = reader.IsDBNull(6) ? null : reader.GetString(6),
                    });
                }
            }

            return stats;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
