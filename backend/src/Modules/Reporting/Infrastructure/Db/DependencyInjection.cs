using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Reporting.Application;

namespace Nordiska.Modules.Reporting.Infrastructure.Db;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IPdfReportGenerator, PdfReportGenerator>();
        services.AddScoped<IReportDataBuilder, ReportDataBuilder>();
        services.AddSingleton<IReportFileStorage, LocalReportFileStorage>();
        services.AddScoped<ITaxReportService, TaxReportService>();
        services.AddScoped<ITaxReportJobRepository, TaxReportJobRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services.AddModulePostgresDbContext<ReportingDbContext>(
            configuration,
            ReportingDatabase.Details);
    }
}