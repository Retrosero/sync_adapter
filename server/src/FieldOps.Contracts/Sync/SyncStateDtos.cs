namespace FieldOps.Contracts.Sync;

public class SyncStateResponse
{
    public Guid TenantId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime? LastRunAt { get; set; }
    public long RowsTotalSynced { get; set; }
    public long RowsInLastRun { get; set; }
    public DateTime? Checkpoint { get; set; }
    public bool IsInitial { get; set; }
    public bool DeadLettered { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}

public class SyncTableResponse
{
    public string TableName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Direction { get; set; } = "Push"; // "Push", "Pull", "Both"
    public bool Enabled { get; set; } = true;
}

public class SyncTablesResponse
{
    public List<SyncTableResponse> Tables { get; set; } = new();
}

public class SyncRunResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string? AgentId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
    public long RowsTotal { get; set; }
    public long RowsSynced { get; set; }
    public long RowsFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCategory { get; set; }
}
