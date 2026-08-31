using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Banking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBankingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(
                BankingDbDetails.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{BankingDbDetails.ConnectionStringName}' was not found.");

        services.AddDbContext<BankingDbContext>(options =>
        {
            PostgreSqlDbContextOptions.Configure(
                options,
                connectionString,
                typeof(BankingDbContext).Assembly.GetName().Name!,
                BankingDbDetails.Schema);
        });

        return services;
    }
}