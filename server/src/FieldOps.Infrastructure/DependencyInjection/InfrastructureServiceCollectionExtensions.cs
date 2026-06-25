using FieldOps.Application.Abstractions;
using FieldOps.Infrastructure.Persistence;
using FieldOps.Infrastructure.Persistence.Interceptors;
using FieldOps.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// PostgreSQL + EF Core + RLS interceptor'larını + uygulama servislerini ekler.
    /// </summary>
    public static IServiceCollection AddFieldOpsInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres bulunamadı.");

        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddSingleton<TenantSessionInterceptor>();

        services.AddDbContext<FieldOpsDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations", "fieldops");
            });
            options.AddInterceptors(
                sp.GetRequiredService<ISaveChangesInterceptor>(),
                sp.GetRequiredService<TenantSessionInterceptor>());
            options.EnableSensitiveDataLogging(false);
        });

        // Application services
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IEventService, EventService>();

        return services;
    }
}
