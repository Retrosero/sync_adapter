using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using FieldOps.Contracts.Sync;
using FieldOps.Contracts.Outbox;
using FieldOps.Contracts.Events;

namespace SyncAdapter.Infrastructure.Remote;

/// <summary>
/// Sunucu (FieldOps API) ile konuşan HTTP istemcisi.
/// Manuel exponential backoff retry (3 deneme, 1s/5s/30s).
/// X-Api-Key header her istekte otomatik eklenir.
/// </summary>
public interface IRemoteSyncClient
{
    Task<SyncPushResponse> PushSyncBatchAsync(SyncPushRequest req, CancellationToken ct);
    Task<OutboxPullResponse> PullOutboxAsync(OutboxPullRequest req, CancellationToken ct);
    Task PushOutboxAckAsync(OutboxAckRequest req, CancellationToken ct);
    Task IngestEventsAsync(AgentEventBatch batch, CancellationToken ct);
    Task<bool> HealthAsync(CancellationToken ct);
}

public class RemoteSyncClient : IRemoteSyncClient
{
    private readonly HttpClient _http;
    private readonly ILogger<RemoteSyncClient> _logger;
    private const int MaxRetries = 3;
    private static readonly int[] BackoffSeconds = { 1, 5, 30 };
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public RemoteSyncClient(HttpClient http, ILogger<RemoteSyncClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<SyncPushResponse> PushSyncBatchAsync(SyncPushRequest req, CancellationToken ct)
    {
        var resp = await SendWithRetryAsync(() => _http.PostAsJsonAsync("/api/v1/sync/push", req, JsonOpts, ct), ct);
        return await resp.Content.ReadFromJsonAsync<SyncPushResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Empty response");
    }

    public async Task<OutboxPullResponse> PullOutboxAsync(OutboxPullRequest req, CancellationToken ct)
    {
        var resp = await SendWithRetryAsync(() => _http.PostAsJsonAsync("/api/v1/outbox/pull", req, JsonOpts, ct), ct);
        return await resp.Content.ReadFromJsonAsync<OutboxPullResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Empty response");
    }

    public async Task PushOutboxAckAsync(OutboxAckRequest req, CancellationToken ct)
    {
        await SendWithRetryAsync(() => _http.PostAsJsonAsync("/api/v1/outbox/ack", req, JsonOpts, ct), ct);
    }

    public async Task IngestEventsAsync(AgentEventBatch batch, CancellationToken ct)
    {
        await SendWithRetryAsync(() => _http.PostAsJsonAsync("/api/v1/agent/events", batch, JsonOpts, ct), ct);
    }

    public async Task<bool> HealthAsync(CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync("/health", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// HTTP isteğini exponential backoff ile tekrar dener.
    /// 5xx ve network hataları retry edilir; 4xx (client error) edilmez.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        HttpResponseMessage? lastResp = null;
        Exception? lastEx = null;

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var resp = await send();
                lastResp = resp;
                if (resp.IsSuccessStatusCode) return resp;
                if ((int)resp.StatusCode >= 400 && (int)resp.StatusCode < 500)
                {
                    // Client error: retry yok
                    resp.EnsureSuccessStatusCode();
                    return resp;
                }
                // 5xx: retry
                _logger.LogWarning("HTTP {Status}, {Attempt}. deneme {Delay}s sonra",
                    (int)resp.StatusCode, attempt + 1, BackoffSeconds[Math.Min(attempt, BackoffSeconds.Length - 1)]);
            }
            catch (HttpRequestException ex)
            {
                lastEx = ex;
                _logger.LogWarning(ex, "HTTP bağlantı hatası, {Attempt}. deneme {Delay}s sonra",
                    attempt + 1, BackoffSeconds[Math.Min(attempt, BackoffSeconds.Length - 1)]);
            }

            if (attempt < MaxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(BackoffSeconds[Math.Min(attempt, BackoffSeconds.Length - 1)]), ct);
            }
        }

        if (lastEx is not null) throw lastEx;
        lastResp?.EnsureSuccessStatusCode();
        return lastResp ?? throw new InvalidOperationException("HTTP call failed without response");
    }
}
