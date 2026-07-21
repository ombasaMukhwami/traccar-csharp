namespace Traccar.Model;

/// <summary>
/// Snapshot of the database engine's own built-in stats views — a diagnostics-page DTO, not a
/// persisted entity (nothing in <c>Traccar.Storage</c> populates or stores this; a controller
/// would query the active provider's system views to fill it in).
/// </summary>
public class DatabaseStats
{
    public long DatabaseSizeBytes { get; set; }

    public int ActiveConnections { get; set; }

    public long TransactionsCommitted { get; set; }

    public long TransactionsRolledBack { get; set; }

    public long BlocksRead { get; set; }

    public long BlocksHit { get; set; }

    public long TuplesInserted { get; set; }

    public long TuplesUpdated { get; set; }

    public long TuplesDeleted { get; set; }

    public List<TableStats> Tables { get; set; } = [];

    public List<PgActivity> Activity { get; set; } = [];

    /// <summary>Percentage of reads served from the buffer cache rather than disk — the
    /// single most-watched health number for a database instance.</summary>
    public double CacheHitRatio => BlocksHit + BlocksRead == 0 ? 100 : (double)BlocksHit / (BlocksHit + BlocksRead) * 100;
}

/// <summary>One table's row/scan counts joined with its on-disk size.</summary>
public class TableStats
{
    public string TableName { get; set; } = string.Empty;

    public long LiveRows { get; set; }

    public long DeadRows { get; set; }

    public long SequentialScans { get; set; }

    public long IndexScans { get; set; }

    public long TotalSizeBytes { get; set; }
}

/// <summary>One currently-open backend session, scoped to this app's database.</summary>
public class PgActivity
{
    public int Pid { get; set; }

    public string? Username { get; set; }

    public string? ApplicationName { get; set; }

    public string? ClientAddress { get; set; }

    public string? State { get; set; }

    public DateTime? QueryStart { get; set; }

    public string? Query { get; set; }
}
