using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Infrastructure;

namespace Nordiska.Modules.Banking.Infrastructure.Db;

public static class DependencyInjection
{
    public static IServiceCollection AddBankingModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModulePostgresDbContext<BankingDbContext>(
            configuration,
            BankingDatabase.Details);

        // Register repositories and services for the banking module (FAQ-style)
        services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ISavingsAccountService, SavingsAccountService>();
        services.AddScoped<ITransactionService, TransactionService>();

        return services;
    }
}