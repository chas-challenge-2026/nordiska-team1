using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Banking.Infrastructure.Db;

public static class DependencyInjection
{
    public static IServiceCollection AddBankingModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddModulePostgresDbContext<BankingDbContext>(
            configuration,
            BankingDatabase.Details);
    }
}