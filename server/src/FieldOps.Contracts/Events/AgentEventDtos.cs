namespace FieldOps.Contracts.Events;

public class AgentEventDto
{
    public string? AgentId { get; set; }
    public string? AgentVersion { get; set; }
    public string Level { get; set; } = "Info";            // Verbose/Debug/Info/Warning/Error/Fatal
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? Category { get; set; }                  // sync/auth/config/system
    public string? TableName { get; set; }
    public Guid? RunId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Dictionary<string, object?>? Context { get; set; }
}

public class AgentEventBatch
{
    public Guid TenantId { get; set; }
    public List<AgentEventDto> Events { get; set; } = new();
}
