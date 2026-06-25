using FieldOps.Api.Endpoints;
using FieldOps.Api.Middleware;
using FieldOps.Contracts.Common;
using FieldOps.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("application", "fieldops-api")
    .CreateLogger();
builder.Host.UseSerilog();

// ─── Services ───────────────────────────────────────────────────────────────
builder.Services.AddFieldOpsInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FieldOps API", Version = "v1" });
});

// CORS — Super Admin SPA için
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" }; // Vite default
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ─── Pipeline ───────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var feature = ctx.Features.Get<IExceptionHandlerFeature>();
    var ex = feature?.Error;
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Unhandled exception: {Path}", ctx.Request.Path);
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new ApiError
    {
        Code = "INTERNAL_ERROR",
        Message = "Beklenmeyen bir hata oluştu.",
        TraceId = ctx.TraceIdentifier,
    });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// API key auth (süper admin + tenant endpoint'leri için)
// Auth middleware (API key + tenant context — kendi middleware'imiz)
app.UseMiddleware<ApiKeyAuthMiddleware>();

// Healthcheck (auth gerekmez)
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }))
   .AllowAnonymous();

// ─── Endpoint haritalama ────────────────────────────────────────────────────
app.MapTenantEndpoints();        // /api/v1/admin/tenants — super admin
app.MapApiKeyEndpoints();        // /api/v1/admin/tenants/{id}/keys
app.MapSyncEndpoints();          // /api/v1/sync/* — Windows ajanı
app.MapOutboxEndpoints();        // /api/v1/outbox/* — Android & Windows ajanı
app.MapEventEndpoints();         // /api/v1/agent/events — Windows ajanı
app.MapDataEndpoints();          // /api/v1/data/* — Android veri çekme

app.Run();

// Program'ın test edilebilir olması için partial class
public partial class Program { }
