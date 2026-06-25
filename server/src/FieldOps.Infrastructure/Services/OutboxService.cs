using System.Text.Json;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Auth;
using FieldOps.Application.Common;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FieldOps.Contracts.Outbox;

namespace FieldOps.Infrastructure.Services;

public class OutboxService : IOutboxService
{
    private readonly FieldOpsDbContext _db;
    private readonly TimeProvider _clock;

    public OutboxService(FieldOpsDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ServiceResult<OutboxPushResponse>> PushAsync(
        TenantContext ctx, OutboxPushRequest req, CancellationToken ct)
    {
        var response = new OutboxPushResponse();
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var item in req.Items)
        {
            try
            {
                // Idempotent insert: aynı key varsa no-op
                var existing = await _db.OutboxItems
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == ctx.TenantId && x.IdempotencyKey == item.IdempotencyKey, ct);

                if (existing is null)
                {
                    var payloadJson = item.Payload is string s ? s : JsonSerializer.Serialize(item.Payload);
                    var entity = new OutboxItem
                    {
                        TenantId = ctx.TenantId,
                        IdempotencyKey = item.IdempotencyKey,
                        DocumentType = item.DocumentType,
                        PayloadJson = payloadJson,
                        DeviceId = item.DeviceId ?? req.DeviceId,
                        Status = OutboxStatus.Pending,
                        CreatedAt = item.CreatedAt == default ? now : item.CreatedAt,
                    };
                    _db.OutboxItems.Add(entity);
                    await _db.SaveChangesAsync(ct);

                    response.Accepted.Add(new OutboxItemAck
                    {
                        IdempotencyKey = item.IdempotencyKey,
                        ServerId = entity.Id,
                        Status = entity.Status.ToString(),
                        AcceptedAt = now,
                    });
                }
                else
                {
                    // Aynı key geldi → duplicate, ACK dön ama yeni insert yok
                    response.Accepted.Add(new OutboxItemAck
                    {
                        IdempotencyKey = item.IdempotencyKey,
                        ServerId = existing.Id,
                        Status = existing.Status.ToString(),
                        AcceptedAt = now,
                    });
                }
            }
            catch (Exception ex)
            {
                response.Rejected.Add(new OutboxItemReject
                {
                    IdempotencyKey = item.IdempotencyKey,
                    Code = "INTERNAL_ERROR",
                    Message = ex.Message,
                });
            }
        }

        return ServiceResult<OutboxPushResponse>.Success(response);
    }

    public async Task<ServiceResult<OutboxPullResponse>> PullAsync(
        TenantContext ctx, OutboxPullRequest req, CancellationToken ct)
    {
        var limit = Math.Clamp(req.Limit, 1, 200);
        var lockSeconds = Math.Clamp(req.LockSeconds, 30, 600);
        var now = _clock.GetUtcNow().UtcDateTime;
        var lockExpiry = now.AddSeconds(lockSeconds);

        // Pending + lock süresi dolmamış kayıtları çek
        var items = await _db.OutboxItems
            .Where(x =>
                x.TenantId == ctx.TenantId &&
                (x.Status == OutboxStatus.Pending ||
                 (x.Status == OutboxStatus.Dispatched && x.LockExpiresAt < now)) &&
                x.Status != OutboxStatus.DeadLettered)
            .OrderBy(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        // Kilitleri set et
        foreach (var item in items)
        {
            item.Status = OutboxStatus.Dispatched;
            item.LockedByAgentId = req.AgentId;
            item.LockedAt = now;
            item.LockExpiresAt = lockExpiry;
            item.DispatchedAt ??= now;
        }
        await _db.SaveChangesAsync(ct);

        var response = new OutboxPullResponse
        {
            HasMore = items.Count == limit,
            Items = items.Select(i => new OutboxPullItem
            {
                Id = i.Id,
                IdempotencyKey = i.IdempotencyKey,
                DocumentType = i.DocumentType,
                Payload = JsonDocument.Parse(i.PayloadJson).RootElement,
                DeviceId = i.DeviceId,
                CreatedAt = i.CreatedAt,
                RetryCount = i.RetryCount,
                LastError = i.LastError,
            }).ToList(),
        };
        return ServiceResult<OutboxPullResponse>.Success(response);
    }

    public async Task<ServiceResult> AckAsync(TenantContext ctx, OutboxAckRequest req, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var itemIds = req.Items.Select(i => i.Id).ToHashSet();
        var items = await _db.OutboxItems
            .Where(x => x.TenantId == ctx.TenantId && itemIds.Contains(x.Id))
            .ToListAsync(ct);

        var itemMap = items.ToDictionary(x => x.Id);

        foreach (var ack in req.Items)
        {
            if (!itemMap.TryGetValue(ack.Id, out var item))
                continue; // başka tenant'ın item'ı — görmezden gel
            if (item.LockedByAgentId is not null && item.LockedByAgentId != req.AgentId)
                continue; // başka ajan kilitlemiş

            if (ack.Success)
            {
                item.Status = OutboxStatus.Acked;
                item.AckedAt = now;
                item.ErpRef = ack.ErpRef;
                item.LastError = null;
                item.RetryCount = 0;
            }
            else
            {
                item.RetryCount++;
                item.LastError = ack.ErrorMessage;
                ErrorCategory cat = ErrorCategory.Unknown;
                if (!string.IsNullOrEmpty(ack.ErrorCategory))
                    Enum.TryParse<ErrorCategory>(ack.ErrorCategory, true, out cat);

                bool isFatal = cat == ErrorCategory.Schema
                            || cat == ErrorCategory.Auth
                            || cat == ErrorCategory.Config;

                // 5 ardışık transient hata VEYA fatal kategori → dead letter
                if (item.RetryCount >= 5 || isFatal)
                {
                    item.Status = OutboxStatus.DeadLettered;
                }
                else
                {
                    item.Status = OutboxStatus.Pending;
                }
            }
            item.LockedByAgentId = null;
            item.LockedAt = null;
            item.LockExpiresAt = null;
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Success();
    }
}
