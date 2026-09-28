using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Nordiska.Reporting.Worker;

public sealed class TaxReportJobNotificationListener(
    IConfiguration configuration,
    JobNotificationSignal signal,
    ILogger<TaxReportJobNotificationListener> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = GetConnectionString(configuration);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);

                await using (var command = new NpgsqlCommand(
                    "LISTEN tax_report_jobs_pending;",
                    connection))
                {
                    await command.ExecuteNonQueryAsync(stoppingToken);
                }

                logger.LogInformation(
                    "Listening for PostgreSQL notifications on {Channel}.",
                    "tax_report_jobs_pending");

                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                    signal.Notify();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "PostgreSQL notification listener failed. Retrying in 5 seconds.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Reporting")
            ?? configuration.GetConnectionString("ReportingDatabase")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("Database");

        if (string.IsNullOrWhiteSpace(connectionString)
            || connectionString.StartsWith("DEVELOPMENT_PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A PostgreSQL connection string is required.");
        }

        return connectionString;
    }
}
