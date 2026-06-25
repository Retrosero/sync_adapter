using FieldOps.Api.Endpoints;
using FieldOps.Application.Abstractions;
using FieldOps.Contracts.Auth;
using FieldOps.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FieldOps.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/admin/tenants")
                   .WithTags("Admin - Tenants");

        grp.MapGet("/", async (int page, int pageSize, ITenantService svc, CancellationToken ct) =>
        {
            var result = await svc.ListAsync(page, pageSize, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        grp.MapGet("/{id:guid}", async (Guid id, ITenantService svc, CancellationToken ct) =>
        {
            var result = await svc.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.NotFound(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        grp.MapPost("/", async (TenantCreateRequest req, ITenantService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(req, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/admin/tenants/{result.Data!.Id}", result.Data)
                : Results.BadRequest(new
                {
                    code = result.ErrorCode,
                    message = result.ErrorMessage,
                    fieldErrors = result.FieldErrors,
                });
        });

        grp.MapPatch("/{id:guid}", async (Guid id, TenantUpdateRequest req, ITenantService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(id, req, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        grp.MapDelete("/{id:guid}", async (Guid id, ITenantService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(id, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        return app;
    }
}
