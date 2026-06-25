using FieldOps.Application.Abstractions;
using FieldOps.Application.Auth;
using FieldOps.Application.Common;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.Infrastructure.Persistence;
using FieldOps.Contracts.Events;

namespace FieldOps.Infrastructure.Services;

public class EventService : IEventService
{
    private readonly FieldOpsDbContext _db;

    public EventService(FieldOpsDbContext db) => _db = db;

    public async Task<ServiceResult> IngestAsync(TenantContext ctx, AgentEventBatch batch, CancellationToken ct)
    {
        if (batch.Events.Count == 0) return ServiceResult.Success();

        var entities = batch.Events.Select(e => new AgentEvent
        {
            TenantId = ctx.TenantId,
            AgentId = e.AgentId,
            AgentVersion = e.AgentVersion,
            Level = Enum.TryParse<EventLevel>(e.Level, true, out var l) ? l : EventLevel.Info,
            Message = e.Message,
            Exception = e.Exception,
            Category = e.Category,
            ContextJson = e.Context is null ? null : System.Text.Json.JsonSerializer.Serialize(e.Context),
            TableName = e.TableName,
            RunId = e.RunId,
            OccurredAt = e.OccurredAt == default ? DateTime.UtcNow : e.OccurredAt,
        }).ToList();

        _db.AgentEvents.AddRange(entities);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Success();
    }
}
