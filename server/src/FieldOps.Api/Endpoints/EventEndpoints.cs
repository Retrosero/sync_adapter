using FieldOps.Application.Abstractions;
using FieldOps.Contracts.Events;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Api.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/agent/events")
                   .WithTags("Agent Events");

        grp.MapPost("/", async (HttpContext http, AgentEventBatch batch,
            IEventService svc, CancellationToken ct) =>
        {
            var tc = http.GetTenantContext();
            var result = await svc.IngestAsync(tc, batch, ct);
            return result.IsSuccess
                ? Results.Accepted()
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        return app;
    }
}
