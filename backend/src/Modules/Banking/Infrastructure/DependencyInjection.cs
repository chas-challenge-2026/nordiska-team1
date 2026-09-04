using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Banking.Infrastructure.DbConfigs;

namespace Nordiska.Modules.Banking.Infrastructure;

/*
    The DependencyInjection classes are used to inject each schema into the database later on in Program.csd

*/
public static class DependencyInjection
{
    public static IServiceCollection AddBankingModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(BankingDbConstants.ConnectionString)
                                ?? throw new InvalidCastException();

        services.AddDbContext<BankingDbContext>(options =>
        {
            options.UseNordiskaPostgres(
                connectionString,
                BankingDbConstants.Schema);
        });

        return services;
    }
}