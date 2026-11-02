using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Infrastructure.Db;
using Nordiska.Reporting.Worker.Execution;
using Nordiska.Reporting.Worker.JobHandlers.AccountStatements;
using Nordiska.Reporting.Worker.JobHandlers.PdfGeneration;
using Nordiska.Reporting.Worker.JobHandlers.TaxReports;

namespace Nordiska.Reporting.Worker.Tests;

public sealed class WorkerServiceRegistrationTests
{
    [Fact]
    public void WorkerRegistration_ResolvesBothProcessorsWithoutBankingServices()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReportingDatabase"] = "Host=localhost;Database=reporting_tests;Username=worker"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddReportingWorkerInfrastructure(configuration);
        services.AddScoped<IPdfBatchGenerator, PdfGenerationService>();
        services.AddSingleton<AnnualTaxReportNativeMapper>();
        services.AddScoped<ITaxReportRenderPipeline, TaxReportRenderPipeline>();
        services.AddScoped<TaxReportJobProcessor>();
        services.AddSingleton<AccountStatementNativeMapper>();
        services.AddScoped<IAccountStatementRenderPipeline, AccountStatementRenderPipeline>();
        services.AddScoped<AccountStatementJobProcessor>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TaxReportJobProcessor>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AccountStatementJobProcessor>());
        Assert.Null(scope.ServiceProvider.GetService<ITaxReportService>());
    }
}
