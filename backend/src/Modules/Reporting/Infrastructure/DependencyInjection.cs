using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.Modules.Reporting.Infrastructure.DbConfigs;
using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Reporting.Infrastructure;
 
public static class DependencyInjection
{
    public static IServiceCollection AddBankingModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(
                ReportDbConstants.ConnectionString)
            ?? throw new InvalidOperationException(
                $"Connection string '{ReportDbConstants.ConnectionString}' was not found.");

        services.AddDbContext<ReportDbContext>(options =>
        {
            options.UseNordiskaPostgres(
                connectionString,
                ReportDbConstants.Schema);
        });

        return services;
    }
}