using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Inbox.Infrastructure;

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

        services.AddScoped<IInboxRepository, InboxRepository>();
        services.AddScoped<IInboxService, InboxService>();

        return services;
    }
}