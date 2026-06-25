using FieldOps.Application.Abstractions;
using FieldOps.Contracts.Sync;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Api.Endpoints;

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/sync")
                   .WithTags("Sync — Windows Agent");

        // ERP → Sunucu: Windows ajanı batch gönderir
        grp.MapPost("/push", async (HttpContext http, SyncPushRequest req,
            ISyncService svc, CancellationToken ct) =>
        {
            var tc = http.GetTenantContext();
            var result = await svc.PushBatchAsync(tc, req, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        // Tüm tabloların sync durumu
        grp.MapGet("/state", async (HttpContext http, ISyncService svc, CancellationToken ct) =>
        {
            var tc = http.GetTenantContext();
            var result = await svc.GetStateAsync(tc, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        // Kullanılabilir tabloların listesi (MVP: sunucu tarafında sabit, tenant-basis değil)
        grp.MapGet("/tables", (HttpContext http) =>
        {
            var tc = http.GetTenantContext();
            if (tc.TenantId == Guid.Empty)
                return Results.Unauthorized();

            var tables = new SyncTablesResponse
            {
                Tables = new List<SyncTableResponse>
                {
                    new() { TableName = "CARI_HESAPLAR",       DisplayName = "Cari Hesaplar",      Direction = "Both" },
                    new() { TableName = "STOK_KODU",           DisplayName = "Stok Kodları",         Direction = "Pull" },
                    new() { TableName = "SALES_ORDERS",        DisplayName = "Satış Siparişleri",    Direction = "Both" },
                    new() { TableName = "SALES_ORDER_LINES",   DisplayName = "Satış Kalemleri",      Direction = "Both" },
                    new() { TableName = "COLLECTIONS",         DisplayName = "Tahsilatlar",          Direction = "Both" },
                    new() { TableName = "CARI_HESAP_HAREKETLERI", DisplayName = "Cari Hareketler", Direction = "Both" },
                    new() { TableName = "STOK_HAREKETLERI",     DisplayName = "Stok Hareketleri",     Direction = "Both" },
                    new() { TableName = "KASALAR",             DisplayName = "Kasalar",               Direction = "Pull" },
                    new() { TableName = "KASALAR_YONETIM",      DisplayName = "Kasa Yönetimi",         Direction = "Pull" },
                    new() { TableName = "BARKOD_TANIMLARI",     DisplayName = "Barkod Tanımları",     Direction = "Pull" },
                }
            };
            return Results.Ok(tables);
        });

        // Son sync_run'lar
        grp.MapGet("/runs", async (int page, int pageSize, HttpContext http,
            ISyncService svc, CancellationToken ct) =>
        {
            var tc = http.GetTenantContext();
            var result = await svc.GetRunsAsync(tc, page, pageSize, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        });

        return app;
    }
}
