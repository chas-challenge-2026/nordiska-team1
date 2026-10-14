using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Infrastructure;

namespace Nordiska.Modules.Faq.Infrastructure.Db;

public static class DependencyInjection
{
    public static IServiceCollection AddFaqModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
         services.AddModulePostgresDbContext<FaqDbContext>(
            configuration,
            FaqDatabase.Details);
        services.AddMemoryCache();
        services.AddSingleton<FaqCacheInvalidator>();
        services.AddScoped<IFaqRepository, FaqRepository>();
        services.AddScoped<FaqService>();

        // The salt has no safe default, so a missing one should stop the app at startup
        services.AddOptions<FaqSearchLogOptions>()
            .Bind(configuration.GetSection(FaqSearchLogOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Salt), "FaqSearchLog:Salt is required.")
            .Validate(o => o.RetentionDays > 0, "FaqSearchLog:RetentionDays must be greater than zero.")
            .Validate(o => o.QueueCapacity > 0, "FaqSearchLog:QueueCapacity must be greater than zero.")
            .Validate(o => o.CleanupIntervalHours > 0, "FaqSearchLog:CleanupIntervalHours must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<FaqSearchLogQueue>();
        services.AddScoped<IFaqSearchLogRepository, FaqSearchLogRepository>();

        // Views use the same salt, queue capacity and retention as the search log
        services.AddSingleton<FaqViewLogQueue>();
        services.AddScoped<IFaqViewLogRepository, FaqViewLogRepository>();
        return services;
    }
}