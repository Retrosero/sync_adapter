using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SyncAdapter.Infrastructure.Mikro;

/// <summary>
/// Mikro ERP'ye (MSSQL) bağlanıp belirli bir tablo için delta veya full pull yapar.
/// Windows Auth (Trusted_Connection) varsayılan; SQL Auth da desteklenir.
/// Sonuç: her satır için kolon adı → değer dictionary'si, primary key ayrı taşınır.
/// </summary>
public interface IMikroClient
{
    Task<bool> TestConnectionAsync(CancellationToken ct);
    Task<TableSnapshot> ReadTableAsync(TableSyncConfig table, DateTime? checkpoint, bool isInitial, CancellationToken ct);
}

public class TableSyncConfig
{
    public string TableName { get; init; } = string.Empty;
    public string SchemaName { get; init; } = "dbo";
    public string[] PrimaryKeyColumns { get; init; } = Array.Empty<string>();
    public string? LastModifiedColumn { get; init; }    // null ise full pull
    public string? RowVersionColumn { get; init; }     // opsiyonel: ROWVERSION takibi
    public int PageSize { get; init; } = 5000;
}

public class TableSnapshot
{
    public string TableName { get; init; } = string.Empty;
    public DateTime? CheckpointReached { get; init; }       // Bu snapshot'taki en yeni last_modified
    public string? RowVersionReached { get; init; }         // Veya ROWVERSION
    public List<Dictionary<string, object?>> Rows { get; init; } = new();
    public bool HasMore { get; init; }
}

public class MikroClient : IMikroClient
{
    private readonly string _connectionString;
    private readonly ILogger<MikroClient> _logger;

    public MikroClient(string connectionString, ILogger<MikroClient> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT 1", conn);
            var result = await cmd.ExecuteScalarAsync(ct);
            _logger.LogInformation("Mikro MSSQL bağlantısı başarılı: {Server}", conn.DataSource);
            return result is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mikro MSSQL bağlantı hatası: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<TableSnapshot> ReadTableAsync(
        TableSyncConfig table, DateTime? checkpoint, bool isInitial, CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();
        DateTime? newCheckpoint = checkpoint;
        string? newRowVersion = null;
        bool hasMore = false;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Sorgu oluştur
        var sql = $@"SELECT * FROM [{table.SchemaName}].[{table.TableName}] WITH (NOLOCK)";
        var parameters = new List<SqlParameter>();
        if (!isInitial && checkpoint.HasValue && !string.IsNullOrEmpty(table.LastModifiedColumn))
        {
            sql += $" WHERE [{table.LastModifiedColumn}] > @checkpoint";
            parameters.Add(new SqlParameter("@checkpoint", checkpoint.Value));
        }
        sql += $" ORDER BY [{table.LastModifiedColumn ?? table.PrimaryKeyColumns[0]}]";
        sql += " OFFSET 0 ROWS FETCH NEXT @pageSize ROWS ONLY";

        parameters.Add(new SqlParameter("@pageSize", table.PageSize + 1)); // +1: has_more tespiti

        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 300;
        foreach (var p in parameters) cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var columnNames = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++) columnNames.Add(reader.GetName(i));

        int count = 0;
        while (await reader.ReadAsync(ct))
        {
            if (count >= table.PageSize)
            {
                hasMore = true;
                break;
            }

            var dict = new Dictionary<string, object?>(columnNames.Count);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var v = reader.GetValue(i);
                dict[columnNames[i]] = v is DBNull ? null : v;
            }
            rows.Add(dict);

            // Checkpoint güncelle
            if (!string.IsNullOrEmpty(table.LastModifiedColumn) &&
                dict.TryGetValue(table.LastModifiedColumn, out var lm) && lm is DateTime dt)
            {
                if (newCheckpoint is null || dt > newCheckpoint) newCheckpoint = dt;
            }
            count++;
        }

        _logger.LogInformation(
            "{Table}: {Count} satır okundu (isInitial={IsInitial}, hasMore={HasMore})",
            table.TableName, count, isInitial, hasMore);

        return new TableSnapshot
        {
            TableName = table.TableName,
            Rows = rows,
            CheckpointReached = newCheckpoint,
            RowVersionReached = newRowVersion,
            HasMore = hasMore,
        };
    }

    public static string BuildConnectionString(MikroConfig cfg)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{cfg.Server},{cfg.Port}",
            InitialCatalog = cfg.Database,
            Encrypt = cfg.Encrypt,
            TrustServerCertificate = cfg.TrustServerCertificate,
            ConnectTimeout = 30,
            ApplicationName = "FieldOps.Agent",
        };
        if (cfg.UseWindowsAuth)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = cfg.User;
            builder.Password = cfg.Password;
        }
        return builder.ConnectionString;
    }
}

public class MikroConfig
{
    public string Server { get; set; } = "GURBUZ";
    public int Port { get; set; } = 1433;
    public string Database { get; set; } = "MikroDB_V15_02";
    public bool UseWindowsAuth { get; set; } = true;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Encrypt { get; set; } = false;
    public bool TrustServerCertificate { get; set; } = true;
    public int CommandTimeoutSeconds { get; set; } = 300;
    public int PoolSize { get; set; } = 10;
}
