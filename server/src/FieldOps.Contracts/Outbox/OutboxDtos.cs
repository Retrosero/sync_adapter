namespace FieldOps.Contracts.Outbox;

/// <summary>
/// Android'den gelen tek bir outbox item. idempotency_key tek seferliktir — aynı key ile gelen
/// tekrar istek no-op olarak işlenir (HTTP 200, yeni insert yok, ACK var).
/// </summary>
public class OutboxPushItem
{
    public string IdempotencyKey { get; set; } = string.Empty;  // UUID
    public string DocumentType { get; set; } = string.Empty;    // "sales_order", "collection"
    public object Payload { get; set; } = new();               // Serbest JSON
    public string? DeviceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OutboxPushRequest
{
    public Guid TenantId { get; set; }
    public string? DeviceId { get; set; }
    public List<OutboxPushItem> Items { get; set; } = new();
}

public class OutboxPushResponse
{
    public List<OutboxItemAck> Accepted { get; set; } = new();
    public List<OutboxItemReject> Rejected { get; set; } = new();
}

public class OutboxItemAck
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid ServerId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime AcceptedAt { get; set; }
}

public class OutboxItemReject
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
