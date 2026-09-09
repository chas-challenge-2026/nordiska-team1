using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Reporting.Infrastructure.Db;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddModulePostgresDbContext<ReportingDbContext>(
            configuration,
            ReportingDatabase.Details);
    }
}