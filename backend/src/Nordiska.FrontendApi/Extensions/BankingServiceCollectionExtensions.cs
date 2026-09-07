using Microsoft.Extensions.DependencyInjection;

namespace Nordiska.FrontendApi.Extensions;

// extension method to register banking services in the dependency injection container
//keeps program.cs clean and organized
public static class BankingServiceCollectionExtensions
{
    public static IServiceCollection AddBankingServices<TSavings, TTransaction>(this IServiceCollection services)
        where TSavings : class, Nordiska.Modules.Banking.Application.ISavingsAccountService
        where TTransaction : class, Nordiska.Modules.Banking.Application.ITransactionService
    {
        services.AddScoped<Nordiska.Modules.Banking.Application.ISavingsAccountService, TSavings>();
        services.AddScoped<Nordiska.Modules.Banking.Application.ITransactionService, TTransaction>();

        return services;
    }
}
