using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Banking.Infrastructure;
using Nordiska.Modules.Faq.Infrastructure.DbConfigs;

namespace Nordiska.Modules.Banking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFaqModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(
                FaqDbConstants.ConnectionString)
            ?? throw new InvalidOperationException(
                $"Connection string '{FaqDbConstants.ConnectionString}' was not found.");

        services.AddDbContext<FaqDbContext>(options =>
        {
            options.UseNordiskaPostgres(
                connectionString,
                FaqDbConstants.Schema);
        });

        return services;
    }
}