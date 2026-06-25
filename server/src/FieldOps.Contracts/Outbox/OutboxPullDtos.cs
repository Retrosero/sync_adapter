namespace FieldOps.Contracts.Outbox;

/// <summary>
/// Windows ajanının sunucudan outbox item çekmesi için endpoint.
/// </summary>
public class OutboxPullRequest
{
    public Guid TenantId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public int Limit { get; set; } = 50;            // max 200
    public int LockSeconds { get; set; } = 120;     // Item kilit süresi
}

public class OutboxPullResponse
{
    public List<OutboxPullItem> Items { get; set; } = new();
    public bool HasMore { get; set; }
}

public class OutboxPullItem
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public object Payload { get; set; } = new();
    public string? DeviceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// Ajan, ERP'ye yazdıktan sonra sunucuya sonucu bildirir.
/// </summary>
public class OutboxAckRequest
{
    public Guid TenantId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public List<OutboxAckItem> Items { get; set; } = new();
}

public class OutboxAckItem
{
    public Guid Id { get; set; }                   // Server'daki outbox item id
    public string IdempotencyKey { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErpRef { get; set; }            // ERP evrak no, vb.
    public string? ErrorMessage { get; set; }
    public string? ErrorCategory { get; set; }
}
