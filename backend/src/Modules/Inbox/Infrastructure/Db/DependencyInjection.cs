using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Inbox.Infrastructure.Db;

public static class DependencyInjection
{
    public static IServiceCollection AddInboxModuleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModulePostgresDbContext<InboxDbContext>(
            configuration,
            InboxDatabase.Details);

        return services;
    }
}