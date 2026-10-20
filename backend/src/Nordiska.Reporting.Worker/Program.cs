using Microsoft.Extensions.Options;
using Nordiska.Modules.Reporting.Infrastructure.Db;
using Nordiska.Reporting.Worker.Execution;
using Nordiska.Reporting.Worker.JobHandlers.AccountStatements;
using Nordiska.Reporting.Worker.JobHandlers.PdfGeneration;
using Nordiska.Reporting.Worker.JobHandlers.TaxReports;
using Nordiska.Reporting.Worker.Lifecycle;
using Nordiska.Reporting.Worker.ResourceManagement;
using Nordiska.Reporting.Worker.Scheduling;

HostApplicationBuilder builder =
    Host.CreateApplicationBuilder(args);


builder.Services.AddReportingModuleInfrastructure(
    builder.Configuration);

builder.Services.Configure<TaxReportWorkerOptions>(
    builder.Configuration.GetSection(
        "TaxReportWorker"));

builder.Services.Configure<ResourceManagementOptions>(
    builder.Configuration.GetSection("ResourceManagement"));

builder.Services.AddSingleton<IProcessResourceReader, ProcessResourceReader>();

builder.Services.AddScoped<
    IPdfBatchGenerator,
    PdfGenerationService>();

builder.Services.AddSingleton<
    AnnualTaxReportNativeMapper>();

builder.Services.AddScoped<
    ITaxReportRenderPipeline,
    TaxReportRenderPipeline>();

builder.Services.AddScoped<
    TaxReportJobProcessor>();

builder.Services.AddSingleton<
    AccountStatementNativeMapper>();

builder.Services.AddScoped<
    IAccountStatementRenderPipeline,
    AccountStatementRenderPipeline>();

builder.Services.AddScoped<
    AccountStatementJobProcessor>();

builder.Services.AddSingleton<IReportingJobWakeSignal>(
    serviceProvider =>
    {
        TaxReportWorkerOptions options = serviceProvider
            .GetRequiredService<IOptions<TaxReportWorkerOptions>>()
            .Value;

        return new ReportingJobWakeSignal(
            options.Concurrency);
    });

builder.Services.AddSingleton<
    IReportingJobNotificationSource,
    PostgresReportingJobNotificationSource>();

builder.Services.AddSingleton<
    IWorkerLoop,
    WorkerLoop>();

builder.Services.AddSingleton<WorkerLifecycle>();

builder.Services.AddSingleton<
    IReportingJobScheduler,
    ReportingJobScheduler>();

builder.Services.AddHostedService<
    ReportingJobNotificationListener>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<ResourceMonitor>();

IHost host = builder.Build();

await host.RunAsync();
