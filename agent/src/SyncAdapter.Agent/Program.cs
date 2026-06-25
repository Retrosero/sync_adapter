using SyncAdapter.Agent.Workers;
using SyncAdapter.Core.Sync;
using SyncAdapter.Infrastructure.Mikro;
using SyncAdapter.Infrastructure.Remote;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.EventLog;

// ─── Serilog setup (dosya + EventLog + console) ───────────────────────────
var dataDir = LocalStateFileStore.DefaultDataDir();
Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(Path.Combine(dataDir, "Logs"));

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "FieldOps.Agent")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(dataDir, "Logs", "fieldops-agent-.log"),
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true,
        fileSizeLimitBytes: 10 * 1024 * 1024,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
#if WINDOWS
    .WriteTo.EventLog(
        source: "FieldOps.Agent",
        logName: "Application",
        restrictedToMinimumLevel: LogEventLevel.Warning)
#endif
    .CreateLogger();

try
{
    Log.Information("FieldOps Agent başlatılıyor…");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: true);

    // Configuration
    builder.Services.Configure<AgentOptions>(builder.Configuration);

    // AgentId yoksa otomatik hesapla
    var opts = builder.Configuration.Get<AgentOptions>() ?? new AgentOptions();
    if (string.IsNullOrEmpty(opts.AgentId))
        opts.AgentId = LocalStateFileStore.ComputeAgentId();
    if (string.IsNullOrEmpty(opts.DataDir))
        opts.DataDir = LocalStateFileStore.DefaultDataDir();

    // HTTP client (sunucu) — Polly retry
    builder.Services.AddHttpClient<IRemoteSyncClient, RemoteSyncClient>(client =>
    {
        client.BaseAddress = new Uri(opts.ServerBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Add("X-Api-Key", opts.ApiKey);
        client.DefaultRequestHeaders.Add("User-Agent", $"FieldOps.Agent/{opts.AgentId}");
    });

    // Mikro MSSQL client (singleton — connection string bir kez çözülür)
    var mikroConnString = MikroClient.BuildConnectionString(opts.Mikro);
    builder.Services.AddSingleton<IMikroClient>(_ => new MikroClient(mikroConnString,
        LoggerFactory.Create(b => b.AddSerilog()).CreateLogger<MikroClient>()));

    // Lokal state store
    builder.Services.AddSingleton<ILocalStateStore>(_ => new LocalStateFileStore(opts.DataDir));

    // Sync worker (Windows Service olarak çalışacak)
    builder.Services.AddHostedService<SyncWorker>();

    // Windows Service olarak host et
    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent beklenmeyen hata ile durdu: {Message}", ex.Message);
}
finally
{
    await Log.CloseAndFlushAsync();
}
