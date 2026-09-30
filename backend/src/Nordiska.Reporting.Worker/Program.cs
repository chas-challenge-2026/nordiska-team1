using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Nordiska.Modules.Reporting.Infrastructure.Db;
using Nordiska.Reporting.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBankingModuleInfrastructure(builder.Configuration);
builder.Services
    .AddIdentityCore<Customer>()
    .AddRoles<IdentityRole<long>>()
    .AddEntityFrameworkStores<BankingDbContext>();
builder.Services.AddReportingModuleInfrastructure(builder.Configuration);
builder.Services.AddScoped<TaxReportJobProcessor>();
builder.Services.AddSingleton<JobNotificationSignal>();
builder.Services.AddSingleton<TaxReportJobNotificationListener>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<TaxReportJobNotificationListener>());
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();



