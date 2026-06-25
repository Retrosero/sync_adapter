using FieldOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Endpoints;

/// <summary>
/// Android'in sunucudan veri çekeceği endpoint'ler. Şu an MVP: basit sayfalı liste.
/// F4'te generic sync_data üzerinden tablo adına göre filtreleme yapılır,
/// column-based projection ile gereksiz alanlar response'a girmez.
/// </summary>
public static class DataEndpoints
{
    public static IEndpointRouteBuilder MapDataEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/data")
                   .WithTags("Data — Android");

        // /api/v1/data/{tableName}?since={iso}&page=1&pageSize=200
        grp.MapGet("/{tableName}", async (HttpContext http, string tableName,
            DateTime? since, int page, int pageSize,
            FieldOpsDbContext db, CancellationToken ct) =>
        {
            var tc = http.GetTenantContext();
            page = Math.Max(1, page == 0 ? 1 : page);
            pageSize = Math.Clamp(pageSize == 0 ? 200 : pageSize, 1, 1000);

            // RLS zaten devrede — TenantId otomatik filtreleniyor.
            var query = db.SyncData
                .Where(d => d.TenantId == tc.TenantId && d.TableName == tableName.ToLowerInvariant());
            if (since.HasValue) query = query.Where(d => d.SyncedAt > since.Value);

            var total = await query.CountAsync(ct);
            var rows = await query
                .OrderBy(d => d.SyncedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.Id,
                    d.TableName,
                    d.SourcePk,
                    Payload = d.PayloadJson,    // jsonb → string
                    d.SourceModifiedAt,
                    d.SyncedAt,
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                tableName,
                total,
                page,
                pageSize,
                rows,
            });
        });

        // Tablo listesi (Android hangi tablolar var görebilsin)
        grp.MapGet("/", async (HttpContext http, FieldOpsDbContext db, CancellationToken ct) =>
        {
            var tc = http.GetTenantContext();
            var tables = await db.SyncData
                .Where(d => d.TenantId == tc.TenantId)
                .GroupBy(d => d.TableName)
                .Select(g => new
                {
                    TableName = g.Key,
                    Count = g.Count(),
                    LastSyncedAt = g.Max(d => d.SyncedAt),
                })
                .ToListAsync(ct);
            return Results.Ok(tables);
        });

        return app;
    }
}
