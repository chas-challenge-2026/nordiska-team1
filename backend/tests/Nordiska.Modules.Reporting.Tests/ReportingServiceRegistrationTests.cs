using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Infrastructure;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class ReportingServiceRegistrationTests
{
    [Fact]
    public void ApiRegistration_StillResolvesLegacyTaxReportService()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReportingDatabase"] = "Host=localhost;Database=reporting_tests;Username=api"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddSingleton(Mock.Of<ISavingsAccountService>());
        services.AddSingleton(Mock.Of<ICustomerService>());
        services.AddSingleton(Mock.Of<ITransactionService>());
        services.AddReportingModuleInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.IsType<TaxReportService>(scope.ServiceProvider.GetRequiredService<ITaxReportService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAnnualTaxReportService>());
    }
}
