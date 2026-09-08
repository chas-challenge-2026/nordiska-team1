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
        // Register an in-memory cache and a cached repository wrapper for FAQ entries
        services.AddMemoryCache();
        services.AddScoped<FaqRepository>();
        services.AddScoped<CachedFaqRepository>();
        services.AddScoped<IFaqRepository>(sp => sp.GetRequiredService<CachedFaqRepository>());
        services.AddScoped<FaqService>();
        return services;
    }
}