using FieldOps.Application.Abstractions;
using FieldOps.Contracts.Auth;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Api.Endpoints;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/admin/tenants/{tenantId:guid}/keys")
                   .WithTags("Admin - API Keys");

        grp.MapGet("/", async (Guid tenantId, IApiKeyService svc, CancellationToken ct) =>
        {
            var result = await svc.ListAsync(tenantId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        grp.MapPost("/", async (Guid tenantId, ApiKeyCreateRequest req,
            IApiKeyService svc, HttpContext http, CancellationToken ct) =>
        {
            var createdBy = http.User?.Identity?.Name ?? "admin";
            var result = await svc.CreateAsync(tenantId, req, createdBy, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/admin/tenants/{tenantId}/keys/{result.Data!.Id}", result.Data)
                : Results.BadRequest(new
                {
                    code = result.ErrorCode,
                    message = result.ErrorMessage,
                    fieldErrors = result.FieldErrors,
                });
        });

        grp.MapPost("/{keyId:guid}/revoke", async (Guid tenantId, Guid keyId,
            IApiKeyService svc, CancellationToken ct) =>
        {
            var result = await svc.RevokeAsync(tenantId, keyId, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        return app;
    }
}
