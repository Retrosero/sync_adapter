using System.Text.Json;

namespace SyncAdapter.Core.Sync;

/// <summary>
/// Lokal dosyada tutulan sync state. Her tablo için son başarılı checkpoint + retry count.
/// F4'te server tarafında (sync_state tablosu) tutulacak, şimdilik lokal.
/// </summary>
public class LocalSyncState
{
    public string TableName { get; set; } = string.Empty;
    public DateTime? LastCheckpoint { get; set; }
    public bool IsInitial { get; set; } = true;
    public int RetryCount { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastError { get; set; }
    public bool DeadLettered { get; set; }
}

public class LocalSyncStateFile
{
    public string AgentId { get; set; } = string.Empty;
    public Dictionary<string, LocalSyncState> Tables { get; set; } = new();
}

public interface ILocalStateStore
{
    Task<LocalSyncState> GetAsync(string tableName, CancellationToken ct);
    Task SaveAsync(string tableName, LocalSyncState state, CancellationToken ct);
    Task<List<LocalSyncState>> ListAsync(CancellationToken ct);
}

public class LocalStateFileStore : ILocalStateStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private LocalSyncStateFile _cache = new();
    private bool _loaded;

    public LocalStateFileStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "sync_state.json");
    }

    public static string DefaultDataDir()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "FieldOps", "Agent");
    }

    public static string ComputeAgentId()
    {
        // Makine fingerprint: MachineName + UserName + OS sürümü + stable random
        var machine = Environment.MachineName;
        var user = Environment.UserName;
        var os = Environment.OSVersion.VersionString;
        var raw = $"{machine}|{user}|{os}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        if (_loaded) return;
        await _lock.WaitAsync(ct);
        try
        {
            if (_loaded) return;
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath, ct);
                _cache = JsonSerializer.Deserialize<LocalSyncStateFile>(json) ?? new LocalSyncStateFile();
            }
            if (string.IsNullOrEmpty(_cache.AgentId))
                _cache.AgentId = ComputeAgentId();
            _loaded = true;
        }
        finally { _lock.Release(); }
    }

    public async Task<LocalSyncState> GetAsync(string tableName, CancellationToken ct)
    {
        await LoadAsync(ct);
        if (!_cache.Tables.TryGetValue(tableName, out var state))
        {
            state = new LocalSyncState { TableName = tableName, IsInitial = true };
            _cache.Tables[tableName] = state;
        }
        return state;
    }

    public async Task SaveAsync(string tableName, LocalSyncState state, CancellationToken ct)
    {
        await LoadAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            _cache.Tables[tableName] = state;
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json, ct);
        }
        finally { _lock.Release(); }
    }

    public async Task<List<LocalSyncState>> ListAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return _cache.Tables.Values.ToList();
    }
}
