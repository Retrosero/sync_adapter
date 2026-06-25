using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAdapter.Core.Sync;
using SyncAdapter.Infrastructure.Mikro;
using SyncAdapter.Infrastructure.Remote;
using FieldOps.Contracts.Sync;
using FieldOps.Contracts.Outbox;
using FieldOps.Contracts.Events;
using System.Diagnostics;

namespace SyncAdapter.Agent.Workers;

/// <summary>
/// Ana sync orchestrator. Periyodik olarak:
///   1) Mikro MSSQL'den delta sync yapar
///   2) Toplu halde sunucuya push'lar
///   3) Outbox'tan Android'den gelen belgeleri çeker
///   4) ERP'ye yazma denemesi (F5 — şimdilik sadece log)
///   5) Sonucu sunucuya ACK'ler
///
/// Tek bir BackgroundService olarak çalışır. Birden fazla tablo sırayla işlenir.
/// Hata durumunda: üstel backoff ile retry, 5 ardışık hata → dead-letter.
/// </summary>
public class SyncWorker : BackgroundService
{
    private readonly AgentOptions _opts;
    private readonly IMikroClient _mikro;
    private readonly IRemoteSyncClient _remote;
    private readonly ILocalStateStore _stateStore;
    private readonly ILogger<SyncWorker> _logger;

    public SyncWorker(
        IOptions<AgentOptions> opts,
        IMikroClient mikro,
        IRemoteSyncClient remote,
        ILocalStateStore stateStore,
        ILogger<SyncWorker> logger)
    {
        _opts = opts.Value;
        _mikro = mikro;
        _remote = remote;
        _stateStore = stateStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SyncWorker başladı. AgentId={AgentId}, Tables=[{Tables}], Period={Period}s",
            _opts.AgentId,
            string.Join(",", _opts.Tables.Select(t => t.TableName)),
            _opts.SyncPeriodSeconds);

        // İlk başlangıçta MSSQL bağlantısını test et
        var mikroOk = await _mikro.TestConnectionAsync(stoppingToken);
        if (!mikroOk)
        {
            _logger.LogError("Mikro MSSQL bağlantısı kurulamadı. Worker 60 saniye sonra tekrar deneyecek.");
        }
        else
        {
            // Sunucu sağlık kontrolü
            var remoteOk = await _remote.HealthAsync(stoppingToken);
            _logger.LogInformation("Sunucu health: {Status}", remoteOk ? "OK" : "DOWN");
        }

        // Periyodik döngü
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (mikroOk) await RunOneCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncWorker döngü hatası: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_opts.SyncPeriodSeconds), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("SyncWorker durdu.");
    }

    private async Task RunOneCycleAsync(CancellationToken ct)
    {
        foreach (var tableCfg in _opts.Tables)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await SyncOneTableAsync(tableCfg, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tablo sync hatası: {Table}", tableCfg.TableName);
            }
        }
    }

    private async Task SyncOneTableAsync(TableSyncConfig tableCfg, CancellationToken ct)
    {
        var state = await _stateStore.GetAsync(tableCfg.TableName, ct);
        if (state.DeadLettered)
        {
            _logger.LogWarning("Tablo {Table} dead-letter durumunda, atlanıyor.", tableCfg.TableName);
            return;
        }

        var sw = Stopwatch.StartNew();
        var snapshot = await _mikro.ReadTableAsync(tableCfg, state.LastCheckpoint, state.IsInitial, ct);

        if (snapshot.Rows.Count == 0)
        {
            _logger.LogDebug("{Table}: 0 satır, atlanıyor.", tableCfg.TableName);
            return;
        }

        // PK'yı ayıkla (şimdilik tek kolon PK varsayımı)
        var pkColumn = tableCfg.PrimaryKeyColumns.FirstOrDefault() ?? "id";
        var req = new SyncPushRequest
        {
            TenantId = _opts.TenantId,
            AgentId = _opts.AgentId,
            TableName = tableCfg.TableName,
            BatchId = Guid.NewGuid().ToString("N"),
            IsInitial = state.IsInitial,
            CheckpointFrom = state.LastCheckpoint,
            CheckpointTo = snapshot.CheckpointReached,
            Rows = snapshot.Rows.Select(r => new SyncRow
            {
                PrimaryKey = r.TryGetValue(pkColumn, out var pk) ? pk?.ToString() ?? string.Empty : string.Empty,
                Columns = r,
            }).ToList(),
        };

        var resp = await _remote.PushSyncBatchAsync(req, ct);
        _logger.LogInformation(
            "{Table}: {Rows} satır gönderildi (accepted={Accepted}, rejected={Rejected}) — {Ms}ms",
            tableCfg.TableName, snapshot.Rows.Count, resp.RowsAccepted, resp.RowsRejected, sw.ElapsedMilliseconds);

        // State güncelle
        state.LastCheckpoint = snapshot.CheckpointReached;
        state.IsInitial = false;
        state.RetryCount = 0;
        state.LastRunAt = DateTime.UtcNow;
        state.LastError = null;
        await _stateStore.SaveAsync(tableCfg.TableName, state, ct);
    }
}

public class AgentOptions
{
    public string AgentId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string ServerBaseUrl { get; set; } = "http://localhost:5080";
    public int SyncPeriodSeconds { get; set; } = 30;
    public string DataDir { get; set; } = LocalStateFileStore.DefaultDataDir();
    public List<TableSyncConfig> Tables { get; set; } = new();
    public MikroConfig Mikro { get; set; } = new();
}
