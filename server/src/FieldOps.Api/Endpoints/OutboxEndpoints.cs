using FieldOps.Application.Abstractions;
using FieldOps.Contracts.Outbox;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Api.Endpoints;

public static class OutboxEndpoints
{
    public static IEndpointRouteBuilder MapOutboxEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/outbox")
                   .WithTags("Outbox");

        // Android → Sunucu: outbox item'ları kuyruğa al
        grp.MapPost("/push", async (HttpContext http, OutboxPushRequest req,
            IOutboxService svc, CancellationToken ct) =>
        {
            var tc = http.GetTenantContext();
            var result = await svc.PushAsync(tc, req, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        // Windows ajanı pending item'ları çeker
        grp.MapPost("/pull", async (HttpContext http, OutboxPullRequest req,
            IOutboxService svc, CancellationToken ct) =>
        {
            var tc = http.GetTenantContext();
            var result = await svc.PullAsync(tc, req, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        // Windows ajanı ERP yazma sonucunu bildirir
        grp.MapPost("/ack", async (HttpContext http, OutboxAckRequest req,
            IOutboxService svc, CancellationToken ct) =>
        {
            var tc = http.GetTenantContext();
            var result = await svc.AckAsync(tc, req, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        return app;
    }
}
