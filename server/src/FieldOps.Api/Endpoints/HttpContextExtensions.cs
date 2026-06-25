using FieldOps.Application.Auth;

namespace FieldOps.Api.Endpoints;

/// <summary>
/// HttpContext'ten TenantContext'i güvenli şekilde alır.
/// Bulamazsa 500 Internal Error fırlatır (middleware'ın atladığı bir yere endpoint düşmüş demektir).
/// </summary>
public static class HttpContextExtensions
{
    public static TenantContext GetTenantContext(this HttpContext ctx)
    {
        var tc = ctx.Items["TenantContext"] as TenantContext;
        if (tc is null)
        {
            throw new InvalidOperationException(
                "TenantContext bulunamadı. Endpoint'ı /api/* altında mı tanımladınız?");
        }
        return tc;
    }
}
