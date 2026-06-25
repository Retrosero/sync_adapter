namespace FieldOps.Contracts.Auth;

public class TenantCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;       // Unique kısa kod
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? MikroServer { get; set; }               // "GURBUZ" gibi
    public string? MikroDatabase { get; set; }            // "MikroDB_V15_02"
    public string? Notes { get; set; }
}

public class TenantUpdateRequest
{
    public string? Name { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? MikroServer { get; set; }
    public string? MikroDatabase { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}

public class TenantResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? MikroServer { get; set; }
    public string? MikroDatabase { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ApiKeyCount { get; set; }
}
