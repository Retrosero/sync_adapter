namespace FieldOps.Contracts.Auth;

public class ApiKeyCreateRequest
{
    public string? Label { get; set; }                     // "windows-ajan-1", "android-tablo-7"
    public string? AgentId { get; set; }                   // Windows ajan fingerprint (varsa)
    public DateTime? ExpiresAt { get; set; }
    public string Scope { get; set; } = "Tenant";          // "Tenant" | "Admin"
}

/// <summary>
/// API key SADECE bu response'ta düz metin olarak döner.
/// DB'de SHA-256 hash saklanır, bu yüzden bu response'u client kaydetmeli — bir daha gösterilmez.
/// </summary>
public class ApiKeyCreatedResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PlainKey { get; set; } = string.Empty;   // "fo_live_a1b2c3d4e5..." — sadece bu seferlik
    public string KeyPrefix { get; set; } = string.Empty;  // Görüntüleme için
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class ApiKeyResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? AgentId { get; set; }
    public string Scope { get; set; } = "Tenant";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
