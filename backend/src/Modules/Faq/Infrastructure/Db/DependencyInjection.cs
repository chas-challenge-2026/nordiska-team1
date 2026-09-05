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
        services.AddScoped<IFaqRepository, FaqRepository>();
        services.AddScoped<FaqService>();
        return services;
    }
}