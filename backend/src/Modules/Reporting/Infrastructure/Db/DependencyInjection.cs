using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Infrastructure.Db.Queries;

namespace Nordiska.Modules.Reporting.Infrastructure.Db;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IPdfReportGenerator, PdfReportGenerator>();
        services.AddScoped<ITaxReportService, TaxReportService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        /* This query will calculate tax data for reports. Its better to let SQL calculate instead of our backend server.*/
        services.AddScoped<AnnualTaxCalculationQuery>();

        return services.AddModulePostgresDbContext<ReportingDbContext>(
            configuration,
            ReportingDatabase.Details);
    }
}