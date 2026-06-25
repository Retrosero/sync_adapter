namespace FieldOps.Domain.Entities;

/// <summary>
/// Android cihaz kaydı. Tenant'a bağlı. Cihaz kayıp/çalınma durumunda uzaktan devre dışı bırakılabilir.
/// Şu an opsiyonel kullanım için, zorunlu auth header'ı tenant_id+api_key olduğu için kayıt zorunlu değil.
/// </summary>
public class AndroidDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string DeviceId { get; set; } = string.Empty;   // Android device unique id
    public string? Name { get; set; }                       // "Ahmet'in tableti"
    public string? Model { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ApiKeyId { get; set; }                  // Bu cihazın kullandığı api key
}
