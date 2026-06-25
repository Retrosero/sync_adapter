using FieldOps.Application.Abstractions;
using FieldOps.Application.Auth;
using FieldOps.Contracts.Common;

namespace FieldOps.Api.Middleware;

/// <summary>
/// X-Api-Key header'ını kontrol eder, TenantContext'i HttpContext.Items'a koyar.
/// /health ve /swagger/* gibi endpoint'leri anonim bırakır.
/// Süper admin (Admin scope) ve normal tenant (Tenant scope) aynı middleware'den geçer;
/// tenant context'ten BypassRls'i alır, RLS interceptor bunu kullanır.
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, IApiKeyService keyService, ILogger<ApiKeyAuthMiddleware> logger)
    {
        // Anonim endpoint'ler
        var path = ctx.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path == "/")
        {
            await _next(ctx);
            return;
        }

        // Sadece /api/* isteklerde auth zorunlu
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) ||
            string.IsNullOrWhiteSpace(apiKey))
        {
            await Reject(ctx, StatusCodes.Status401Unauthorized, "MISSING_API_KEY", "X-Api-Key header zorunlu.");
            return;
        }

        var result = await keyService.ValidateAsync(apiKey.ToString(), ctx.RequestAborted);
        if (!result.IsSuccess || result.Data is null)
        {
            await Reject(ctx, StatusCodes.Status401Unauthorized,
                result.ErrorCode ?? "INVALID_KEY",
                result.ErrorMessage ?? "API key geçersiz.");
            return;
        }

        // TenantContext'i HttpContext'e koy — interceptor ve endpoint'ler buradan alır
        ctx.Items["TenantId"] = result.Data.TenantId;
        ctx.Items["BypassRls"] = result.Data.BypassRls;
        ctx.Items["ApiKeyId"] = result.Data.ApiKeyId;
        ctx.Items["TenantContext"] = result.Data;

        // Last used at güncellemesini arka plana at (best effort)
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = ctx.RequestServices.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
                await svc.TouchAsync(result.Data.ApiKeyId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "TouchAsync failed (best effort)");
            }
        });

        await _next(ctx);
    }

    private static async Task Reject(HttpContext ctx, int status, string code, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new ApiError
        {
            Code = code,
            Message = message,
            TraceId = ctx.TraceIdentifier,
        });
    }
}
