using Microsoft.Extensions.DependencyInjection;
namespace Nordiska.FrontendApi.Extensions;
/// <summary>
/// Extension methods for registering banking services into the DI container.
/// </summary>
public static class BankingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the concrete savings and transaction service implementations.
    /// </summary>
    public static IServiceCollection AddBankingServices<TSavings, TTransaction>(this IServiceCollection services)
        where TSavings : class, Nordiska.Modules.Banking.Application.ISavingsAccountService
        where TTransaction : class, Nordiska.Modules.Banking.Application.ITransactionService
    {
        services.AddScoped<Nordiska.Modules.Banking.Application.ISavingsAccountService, TSavings>();
        services.AddScoped<Nordiska.Modules.Banking.Application.ITransactionService, TTransaction>();
        return services;
    }
}
