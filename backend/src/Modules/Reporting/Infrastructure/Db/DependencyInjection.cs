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

        services.AddScoped<
            IAnnualTaxCalculationQuery,
            AnnualTaxCalculationQuery>();

        services.AddScoped<
            IAnnualTaxReportRepository,
            AnnualTaxReportRepository>();

        services.AddScoped<
            IAnnualTaxReportService,
            AnnualTaxReportService>();

        services.AddSingleton<
            ITaxReportPayloadHasher,
            TaxReportPayloadHasher>();
        services.AddScoped<
            ITaxReportJobRepository,
            TaxReportJobRepository>();

        services.AddScoped<
            ITaxReportCompletionRepository,
            TaxReportCompletionRepository>();

        services.AddScoped<
            ITaxReportSnapshotVerifier,
            TaxReportSnapshotVerifier>();

        services.AddScoped<
            IAnnualTaxReportQueryService,
            AnnualTaxReportQueryService>();

        services.AddScoped<
            IAccountStatementSourceQuery,
            AccountStatementSourceQuery>();

        services.AddScoped<
            IAccountStatementRepository,
            AccountStatementRepository>();

        services.AddScoped<
            IAccountStatementService,
            AccountStatementService>();

        services.AddSingleton<
            IAccountStatementPayloadHasher,
            AccountStatementPayloadHasher>();

        services.AddScoped<
            IAccountStatementSnapshotVerifier,
            AccountStatementSnapshotVerifier>();

        services.AddScoped<
            IAccountStatementJobRepository,
            AccountStatementJobRepository>();

        services.AddScoped<
            IAccountStatementCompletionRepository,
            AccountStatementCompletionRepository>();

        services.AddScoped<
            IAccountStatementQueryService,
            AccountStatementQueryService>();

        services.Configure<ReportDocumentStorageOptions>(
            configuration.GetSection(
                "ReportDocumentStorage"));

        services.AddSingleton<
            IReportDocumentStorage,
            FileSystemReportDocumentStorage>();
        services.AddSingleton(TimeProvider.System);

        return services.AddModulePostgresDbContext<ReportingDbContext>(
            configuration,
            ReportingDatabase.Details);
    }
}
