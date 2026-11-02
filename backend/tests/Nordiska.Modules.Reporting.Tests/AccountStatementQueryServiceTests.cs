using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure;
using Nordiska.Modules.Reporting.Infrastructure.Db;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class AccountStatementQueryServiceTests
{
    [Fact]
    public async Task GetStatusAsync_OwnerGetsStatus_OtherCustomerGetsNull()
    {
        await using ReportingDbContext db = CreateDbContext();
        AccountStatement statement = CreateStatement(42);
        db.AccountStatements.Add(statement);
        await db.SaveChangesAsync();
        AccountStatementJob job = AccountStatementJob.Create(statement.Id, DateTimeOffset.UtcNow);
        db.AccountStatementJobs.Add(job);
        await db.SaveChangesAsync();

        var service = new AccountStatementQueryService(db, new StubStorage([]));

        AccountStatementJobStatus? owned = await service.GetStatusAsync(42, job.Id, CancellationToken.None);
        AccountStatementJobStatus? other = await service.GetStatusAsync(99, job.Id, CancellationToken.None);

        Assert.NotNull(owned);
        Assert.Equal(AccountStatementJobStatuses.Pending, owned.Status);
        Assert.Null(other);
    }

    [Fact]
    public async Task DownloadAsync_OwnerGetsLatestDocument()
    {
        await using ReportingDbContext db = CreateDbContext();
        AccountStatement statement = CreateStatement(42);
        db.AccountStatements.Add(statement);
        await db.SaveChangesAsync();
        byte[] content = "%PDF-test"u8.ToArray();
        GeneratedDocument document = GeneratedDocument.Create(
            "AccountStatement", "application/pdf", "statement.pdf", "statements/1.pdf",
            content.LongLength, "hash", DateTimeOffset.UtcNow);
        db.GeneratedDocuments.Add(document);
        await db.SaveChangesAsync();
        db.AccountStatementDocuments.Add(AccountStatementDocument.Create(statement.Id, document.Id, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var service = new AccountStatementQueryService(db, new StubStorage(content));
        AccountStatementDownload? result = await service.DownloadAsync(42, statement.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("statement.pdf", result.FileName);
        Assert.Equal(content, result.Content);
    }

    [Fact]
    public async Task DownloadAsync_MissingOrForeignDocument_ReturnsNull()
    {
        await using ReportingDbContext db = CreateDbContext();
        AccountStatement statement = CreateStatement(42);
        db.AccountStatements.Add(statement);
        await db.SaveChangesAsync();
        var service = new AccountStatementQueryService(db, new StubStorage([]));

        Assert.Null(await service.DownloadAsync(42, statement.Id, CancellationToken.None));
        Assert.Null(await service.DownloadAsync(99, statement.Id, CancellationToken.None));
    }

    private static ReportingDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static AccountStatement CreateStatement(long customerId) => AccountStatement.Create(
        customerId, 10, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
        "Customer", "ACC-10", "Savings", "SEK", 0, 0, DateTimeOffset.UtcNow, "1.0", "hash", []);

    private sealed class StubStorage(byte[] content) : IReportDocumentStorage
    {
        public Task SaveAsync(string storageKey, ReadOnlyMemory<byte> value, string expectedSha256Hash, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<byte[]> ReadAsync(string storageKey, string expectedSha256Hash, CancellationToken cancellationToken)
            => Task.FromResult(content);
    }
}
