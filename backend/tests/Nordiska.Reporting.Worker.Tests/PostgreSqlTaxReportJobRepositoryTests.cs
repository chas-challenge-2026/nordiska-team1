using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Reporting.Worker.Tests;

public sealed class PostgreSqlTaxReportJobRepositoryTests
{
    [Fact]
    public async Task ClaimNextPendingAsync_ClaimsPendingJobAtomically()
    {
        var connectionString = Environment.GetEnvironmentVariable("REPORTING_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var jobId = Guid.NewGuid();

        await using var db = new ReportingDbContext(options);
        var repository = new TaxReportJobRepository(db);
        await repository.CreateAsync(new TaxReportJob
        {
            Id = jobId,
            CustomerId = 999999,
            AccountId = 999999,
            Year = 2099,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        try
        {
            var claimed = await repository.ClaimNextPendingAsync();

            Assert.NotNull(claimed);
            Assert.Equal(jobId, claimed.Id);
            Assert.Equal("Processing", claimed.Status);
            Assert.NotNull(claimed.StartedAt);
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM reporting.tax_report_jobs WHERE "Id" = {jobId}
                """);
        }
    }
}