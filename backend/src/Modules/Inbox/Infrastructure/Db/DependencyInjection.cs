using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Contracts.Validators;

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

        services.AddScoped<IValidator<CreateThreadRequest>, CreateThreadRequestValidator>();
        services.AddScoped<IValidator<ReplyThreadRequest>, ReplyThreadRequestValidator>();
        services.AddScoped<IValidator<StaffReplyRequest>, StaffReplyRequestValidator>();
        services.AddScoped<IValidator<CreateAdminThreadRequest>, CreateAdminThreadRequestValidator>();

        return services;
    }
}