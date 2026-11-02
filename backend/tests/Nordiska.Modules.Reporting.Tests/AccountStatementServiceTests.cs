using Moq;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class AccountStatementServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RequestAsync_InvalidPeriod_IsRejected(int invalidCase)
    {
        var source = new Mock<IAccountStatementSourceQuery>();
        var repository = new Mock<IAccountStatementRepository>();
        var service = CreateService(source, repository);

        (DateOnly from, DateOnly to) = invalidCase switch
        {
            0 => (default, new DateOnly(2026, 10, 1)),
            1 => (new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 1)),
            2 => (new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 3)),
            _ => (new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1))
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.RequestAsync(
            42, 10, from, to, CancellationToken.None));

        source.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RequestAsync_InFlightStatement_ReturnsExistingJob()
    {
        var statement = CreateStatement();
        var job = AccountStatementJob.Create(statement.Id, Now);
        var source = new Mock<IAccountStatementSourceQuery>();
        var repository = new Mock<IAccountStatementRepository>();
        repository.Setup(r => r.GetInFlightAsync(
                42, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InFlightAccountStatement(statement, job));

        RequestAccountStatementResult result = await CreateService(source, repository)
            .RequestAsync(42, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None);

        Assert.Equal(job.Id, result.JobId);
        Assert.Equal(AccountStatementJobStatuses.Pending, result.Status);
        source.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RequestAsync_MissingSource_ThrowsNotFound()
    {
        var source = new Mock<IAccountStatementSourceQuery>();
        source.Setup(q => q.GetAsync(42, 10, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountStatementSource?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateService(source, new Mock<IAccountStatementRepository>())
            .RequestAsync(42, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None));
    }

    [Fact]
    public async Task RequestAsync_CalculatesBalancesAndPersistsSnapshot()
    {
        var source = new Mock<IAccountStatementSourceQuery>();
        source.Setup(q => q.GetAsync(42, 10, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountStatementSource(
                42, 10, "Anna", "ACC-10", "Savings", "SEK", 125.50m, 25.50m,
                [
                    new AccountStatementSourceEntry(1, Now.AddDays(-2), "deposit", "Deposit", 100m),
                    new AccountStatementSourceEntry(2, Now.AddDays(-1), "interest", "Interest", 0.005m)
                ]));

        var repository = new Mock<IAccountStatementRepository>();
        AccountStatement? saved = null;
        AccountStatementJob job = AccountStatementJob.Create(0, Now);
        repository.Setup(r => r.CreateStatementAndJobAsync(
                It.IsAny<AccountStatement>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<AccountStatement, DateTimeOffset, CancellationToken>((statement, _, _) => saved = statement)
            .ReturnsAsync(() => (saved!, job));

        RequestAccountStatementResult result = await CreateService(source, repository)
            .RequestAsync(42, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), CancellationToken.None);

        Assert.Equal(AccountStatementJobStatuses.Pending, result.Status);
        Assert.NotNull(saved);
        Assert.Equal(-1, saved!.OpeningBalanceMinor);
        Assert.Equal(10000, saved.ClosingBalanceMinor);
        Assert.Equal([10000L, 1L], saved.Entries.Select(entry => entry.AmountMinor));
        Assert.Equal([9999L, 10000L], saved.Entries.Select(entry => entry.BalanceAfterMinor));
    }

    private static AccountStatementService CreateService(
        Mock<IAccountStatementSourceQuery> source,
        Mock<IAccountStatementRepository> repository)
    {
        return new AccountStatementService(
            source.Object,
            repository.Object,
            Mock.Of<IAccountStatementPayloadHasher>(h => h.Compute(It.IsAny<AccountStatementHashPayload>()) == "hash"),
            new FixedTimeProvider(Now));
    }

    private static AccountStatement CreateStatement()
    {
        return AccountStatement.Create(
            42, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
            "Anna", "ACC-10", "Savings", "SEK", 0, 0, Now, "1.0", "hash", []);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
