namespace FieldOps.Contracts.Common;

/// <summary>
/// Standart API hata zarfı. Tüm hata response'lar bu formatta döner.
/// </summary>
public class ApiError
{
    public string Code { get; set; } = string.Empty;       // "TENANT_NOT_FOUND", "API_KEY_INVALID" vb.
    public string Message { get; set; } = string.Empty;    // İnsan-okunabilir mesaj
    public string? Detail { get; set; }                    // Geliştirici için ek bilgi
    public string? TraceId { get; set; }                   // Correlation id
    public Dictionary<string, string[]>? FieldErrors { get; set; } // Validation hataları
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore => Page * PageSize < Total;
}
