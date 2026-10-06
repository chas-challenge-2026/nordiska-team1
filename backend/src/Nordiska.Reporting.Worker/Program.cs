using Nordiska.Modules.Reporting.Infrastructure.Db;
using Nordiska.Modules.Reporting.PdfGeneration;
using Nordiska.Reporting.Worker;
using Nordiska.Reporting.Worker.AccountStatements;
using Nordiska.Reporting.Worker.TaxReports;

HostApplicationBuilder builder =
    Host.CreateApplicationBuilder(args);

builder.Services.AddReportingModuleInfrastructure(
    builder.Configuration);

builder.Services.Configure<TaxReportWorkerOptions>(
    builder.Configuration.GetSection(
        "TaxReportWorker"));

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

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();

await host.RunAsync();
