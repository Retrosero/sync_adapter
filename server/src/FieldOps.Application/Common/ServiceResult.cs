namespace FieldOps.Application.Common;

/// <summary>
/// Tüm servisler için standart sonuç zarfı. Hata durumunda data yerine error bilgisi döner.
/// HTTP katmanında bu zarf ErrorOrResult middleware tarafından JSON'a çevrilir.
/// </summary>
public class ServiceResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string[]>? FieldErrors { get; init; }

    public static ServiceResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static ServiceResult<T> Failure(string code, string message,
        Dictionary<string, string[]>? fieldErrors = null)
        => new() { IsSuccess = false, ErrorCode = code, ErrorMessage = message, FieldErrors = fieldErrors };
}

public class ServiceResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static ServiceResult Success() => new() { IsSuccess = true };
    public static ServiceResult Failure(string code, string message)
        => new() { IsSuccess = false, ErrorCode = code, ErrorMessage = message };
}
