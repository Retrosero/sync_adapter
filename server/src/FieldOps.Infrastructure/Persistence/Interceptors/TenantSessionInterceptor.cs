using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FieldOps.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Her PostgreSQL bağlantısı açıldığında mevcut tenant'ı session'a set eder.
/// PostgreSQL RLS policy'si bu session değişkenini okuyarak satır bazlı erişim kontrolü uygular.
/// Super admin scope'lu bağlantılarda RLS bypass edilir (ayrı bir connection string ile).
/// </summary>
public class TenantSessionInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string TenantSessionKey = "app.current_tenant";
    private const string BypassRlsSessionKey = "app.bypass_rls";

    public TenantSessionInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        // Önce bağlantıyı aç
        var openResult = await base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
        // Sonra session değişkenini set et
        await ApplyTenantSessionAsync(connection, cancellationToken);
        return openResult;
    }

    public override InterceptionResult ConnectionOpening(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        var openResult = base.ConnectionOpening(connection, eventData, result);
        ApplyTenantSessionAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        return openResult;
    }

    private async Task ApplyTenantSessionAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection.State != System.Data.ConnectionState.Open) return;

        var ctx = _httpContextAccessor.HttpContext;
        var tenantId = ctx?.Items["TenantId"] as Guid?;
        var bypassRls = ctx?.Items["BypassRls"] as bool? ?? false;

        if (bypassRls)
        {
            // Super admin: RLS'yi bypass et
            await SetSessionAsync(connection, BypassRlsSessionKey, "true", ct);
        }
        else if (tenantId.HasValue)
        {
            // Normal tenant bağlantısı
            await SetSessionAsync(connection, TenantSessionKey, tenantId.Value.ToString(), ct);
        }
        // tenantId yoksa (örn. login öncesi) — hiçbir şey set etme
    }

    private static async Task SetSessionAsync(DbConnection conn, string key, string value, CancellationToken ct)
    {
        // Parametre olarak geçmek SQL injection'a karşı güvenli
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT set_config(@key, @value, false)";
        var pKey = cmd.CreateParameter();
        pKey.ParameterName = "@key";
        pKey.Value = key;
        cmd.Parameters.Add(pKey);
        var pVal = cmd.CreateParameter();
        pVal.ParameterName = "@value";
        pVal.Value = value;
        cmd.Parameters.Add(pVal);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
