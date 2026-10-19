using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Infrastructure.Db;
using Npgsql;

namespace Nordiska.Reporting.Worker;

internal sealed class PostgresReportingJobNotificationSource
    : IReportingJobNotificationSource
{
    internal const string ChannelName =
        "reporting_jobs_pending";

    private readonly IServiceScopeFactory _scopeFactory;

    public PostgresReportingJobNotificationSource(
        IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task ListenAsync(
        Action listenerReady,
        Action notificationReceived,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            _scopeFactory.CreateAsyncScope();

        ReportingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<ReportingDbContext>();

        if (dbContext.Database.GetDbConnection()
            is not NpgsqlConnection connection)
        {
            throw new InvalidOperationException(
                "Reporting database connection is not PostgreSQL.");
        }

        void HandleNotification(
            object sender,
            NpgsqlNotificationEventArgs notification)
        {
            if (string.Equals(
                    notification.Channel,
                    ChannelName,
                    StringComparison.Ordinal))
            {
                notificationReceived();
            }
        }

        connection.Notification += HandleNotification;

        try
        {
            await connection.OpenAsync(cancellationToken);

            await using NpgsqlCommand command =
                connection.CreateCommand();

            command.CommandText =
                $"LISTEN {ChannelName};";

            await command.ExecuteNonQueryAsync(cancellationToken);

            listenerReady();

            while (!cancellationToken.IsCancellationRequested)
                await connection.WaitAsync(cancellationToken);
        }
        finally
        {
            connection.Notification -= HandleNotification;
        }
    }
}
