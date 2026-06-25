using System.Text.Json;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Auth;
using FieldOps.Application.Common;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FieldOps.Contracts.Sync;
using FieldOps.Contracts.Common;

namespace FieldOps.Infrastructure.Services;

public class SyncService : ISyncService
{
    private readonly FieldOpsDbContext _db;
    private readonly TimeProvider _clock;

    public SyncService(FieldOpsDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ServiceResult<SyncPushResponse>> PushBatchAsync(
        TenantContext ctx, SyncPushRequest req, CancellationToken ct)
    {
        var startedAt = _clock.GetUtcNow().UtcDateTime;
        var run = new SyncRun
        {
            TenantId = ctx.TenantId,
            Direction = SyncDirection.ErpToServer,
            TableName = req.TableName,
            AgentId = req.AgentId,
            BatchId = req.BatchId,
            Status = SyncStatus.InProgress,
            StartedAt = startedAt,
            CheckpointFrom = req.CheckpointFrom?.ToString("O"),
            CheckpointTo = req.CheckpointTo?.ToString("O"),
            RowsTotal = req.Rows.Count,
        };
        _db.SyncRuns.Add(run);

        var response = new SyncPushResponse { BatchId = req.BatchId };
        var errors = new List<string>();

        try
        {
            // 1) sync_state'i getir veya oluştur
            var state = await _db.SyncStates
                .FirstOrDefaultAsync(s => s.TenantId == ctx.TenantId && s.TableName == req.TableName, ct);
            if (state is null)
            {
                state = new SyncState
                {
                    TenantId = ctx.TenantId,
                    TableName = req.TableName,
                    IsInitial = true,
                    LastStatus = SyncStatus.Pending,
                };
                _db.SyncStates.Add(state);
            }

            // 2) Her satırı yaz (upsert). Burada dictionary → JSON dönüşümü yapıyoruz.
            // F4'te specific entity'lere geçirilecek.
            foreach (var row in req.Rows)
            {
                try
                {
                    var existing = await _db.SyncData
                        .FirstOrDefaultAsync(d =>
                            d.TenantId == ctx.TenantId &&
                            d.TableName == req.TableName &&
                            d.SourcePk == row.PrimaryKey, ct);

                    var payloadJson = JsonSerializer.Serialize(row.Columns);
                    DateTime? sourceModified = null;
                    if (row.Columns.TryGetValue("last_modified", out var lm) && lm is not null)
                        sourceModified = ConvertToDateTime(lm);
                    else if (row.Columns.TryGetValue("deg_tarih", out var dt) && dt is not null)
                        sourceModified = ConvertToDateTime(dt);

                    if (existing is null)
                    {
                        _db.SyncData.Add(new SyncData
                        {
                            TenantId = ctx.TenantId,
                            TableName = req.TableName,
                            SourcePk = row.PrimaryKey,
                            PayloadJson = payloadJson,
                            SourceModifiedAt = sourceModified,
                            SyncedAt = _clock.GetUtcNow().UtcDateTime,
                            SyncBatchId = req.BatchId,
                        });
                    }
                    else
                    {
                        existing.PayloadJson = payloadJson;
                        existing.SourceModifiedAt = sourceModified;
                        existing.SyncedAt = _clock.GetUtcNow().UtcDateTime;
                        existing.SyncBatchId = req.BatchId;
                    }
                    response.RowsAccepted++;
                }
                catch (Exception ex)
                {
                    response.RowsRejected++;
                    errors.Add($"row {row.PrimaryKey}: {ex.Message}");
                }
            }

            // 3) state güncelle
            state.LastRunAt = _clock.GetUtcNow().UtcDateTime;
            state.RowsInLastRun = response.RowsAccepted;
            state.RowsTotalSynced += response.RowsAccepted;
            state.CheckpointTs = req.CheckpointTo;
            state.IsInitial = false;
            state.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            state.LastError = null;

            // 4) run kapat
            run.Status = errors.Count == 0
                ? SyncStatus.Ok
                : (response.RowsAccepted > 0 ? SyncStatus.PartialOk : SyncStatus.Failed);
            run.RowsSynced = response.RowsAccepted;
            run.RowsFailed = response.RowsRejected;
            run.FinishedAt = _clock.GetUtcNow().UtcDateTime;
            run.DurationMs = (long)(run.FinishedAt.Value - startedAt).TotalMilliseconds;
            if (errors.Count > 0) run.ErrorMessage = string.Join("; ", errors.Take(3));
            state.LastStatus = run.Status;

            await _db.SaveChangesAsync(ct);
            response.NextCheckpoint = req.CheckpointTo;
            response.Errors = errors;
            return ServiceResult<SyncPushResponse>.Success(response);
        }
        catch (Exception ex)
        {
            run.Status = SyncStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.ErrorCategory = ErrorCategory.Unknown;
            run.FinishedAt = _clock.GetUtcNow().UtcDateTime;
            run.DurationMs = (long)(run.FinishedAt.Value - startedAt).TotalMilliseconds;
            run.RowsSynced = response.RowsAccepted;
            run.RowsFailed = req.Rows.Count - response.RowsAccepted;
            try { await _db.SaveChangesAsync(ct); } catch { /* en azından loglamak için */ }

            return ServiceResult<SyncPushResponse>.Failure("PUSH_FAILED", ex.Message);
        }
    }

    public async Task<ServiceResult<List<SyncStateResponse>>> GetStateAsync(TenantContext ctx, CancellationToken ct)
    {
        var items = await _db.SyncStates
            .AsNoTracking()
            .Where(s => s.TenantId == ctx.TenantId)
            .OrderBy(s => s.TableName)
            .Select(s => new SyncStateResponse
            {
                TenantId = s.TenantId,
                TableName = s.TableName,
                Status = s.LastStatus.ToString(),
                LastRunAt = s.LastRunAt,
                RowsTotalSynced = s.RowsTotalSynced,
                RowsInLastRun = s.RowsInLastRun,
                Checkpoint = s.CheckpointTs,
                IsInitial = s.IsInitial,
                DeadLettered = s.DeadLettered,
                RetryCount = s.RetryCount,
                LastError = s.LastError,
            })
            .ToListAsync(ct);
        return ServiceResult<List<SyncStateResponse>>.Success(items);
    }

    public async Task<ServiceResult<PagedResult<SyncRunResponse>>> GetRunsAsync(
        TenantContext ctx, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.SyncRuns
            .AsNoTracking()
            .Where(r => r.TenantId == ctx.TenantId)
            .OrderByDescending(r => r.StartedAt);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new SyncRunResponse
            {
                Id = r.Id,
                TenantId = r.TenantId,
                Direction = r.Direction.ToString(),
                TableName = r.TableName,
                AgentId = r.AgentId,
                Status = r.Status.ToString(),
                StartedAt = r.StartedAt,
                FinishedAt = r.FinishedAt,
                DurationMs = r.DurationMs,
                RowsTotal = r.RowsTotal,
                RowsSynced = r.RowsSynced,
                RowsFailed = r.RowsFailed,
                ErrorMessage = r.ErrorMessage,
                ErrorCategory = r.ErrorCategory.HasValue ? r.ErrorCategory.Value.ToString() : null,
            })
            .ToListAsync(ct);
        return ServiceResult<PagedResult<SyncRunResponse>>.Success(
            new PagedResult<SyncRunResponse> { Items = items, Total = total, Page = page, PageSize = pageSize });
    }

    private static DateTime? ConvertToDateTime(object value)
    {
        if (value is null) return null;
        if (value is DateTime dt) return dt;
        if (value is JsonElement je)
        {
            if (je.TryGetDateTime(out var d)) return d;
            if (je.ValueKind == JsonValueKind.String && DateTime.TryParse(je.GetString(), out var ds)) return ds;
        }
        if (value is string s && DateTime.TryParse(s, out var dts)) return dts;
        return null;
    }
}
