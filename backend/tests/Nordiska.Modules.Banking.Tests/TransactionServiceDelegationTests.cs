using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;

namespace Nordiska.Modules.Banking.Tests;

public sealed class TransactionServiceDelegationTests
{
    [Theory]
    [InlineData("deposit", 20)]
    [InlineData("withdrawal", -20)]
    [InlineData("withdraw", -20)]
    public async Task Execute_UsesAtomicPostingAndMapsSignedAmount(string type, decimal expectedAmount)
    {
        var transactions = new Mock<ITransactionRepository>(MockBehavior.Strict);
        var accounts = new Mock<ISavingsAccountRepository>(MockBehavior.Strict);
        LedgerEntry? posted = null;
        using var cancellation = new CancellationTokenSource();
        transactions.Setup(repository => repository.CreateLedgerEntry(It.IsAny<LedgerEntry>(), cancellation.Token))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => posted = entry)
            .ReturnsAsync(42);
        var service = new TransactionService(transactions.Object, accounts.Object, NullLogger<TransactionService>.Instance);

        var result = await service.ExecuteAsync(new TransactionRequest(7, type, 20m, "Test posting"), cancellation.Token);

        Assert.Equal(42, result.Id);
        Assert.Equal(expectedAmount, result.Amount);
        Assert.Equal(7, result.AccountId);
        Assert.NotNull(posted);
        Assert.Equal("Test posting", posted.Label);
        Assert.Equal(expectedAmount, posted.Amount);
        transactions.Verify(repository => repository.CreateLedgerEntry(It.IsAny<LedgerEntry>(), cancellation.Token), Times.Once);
        accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Transfer_UsesOneAtomicRepositoryOperation()
    {
        var transactions = new Mock<ITransactionRepository>(MockBehavior.Strict);
        var accounts = new Mock<ISavingsAccountRepository>(MockBehavior.Strict);
        var request = new TransferRequest(1, 2, 80m);
        var correlation = Guid.NewGuid();
        transactions.Setup(repository => repository.CreateTransferAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerEntry
            {
                Id = 50, AccountId = 1, TargetAccountId = 2, Type = "transfer", Amount = -80m,
                CorrelationId = correlation, CreatedAt = DateTime.UtcNow
            });
        var service = new TransactionService(transactions.Object, accounts.Object, NullLogger<TransactionService>.Instance);

        var result = await service.TransferAsync(request);

        Assert.Equal(50, result.Id);
        Assert.Equal(-80m, result.Amount);
        Assert.Equal(correlation, result.CorrelationId);
        transactions.Verify(repository => repository.CreateTransferAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PlannedProcessing_DelegatesTheEntireOccurrenceWithUtcCutoff()
    {
        var transactions = new Mock<ITransactionRepository>(MockBehavior.Strict);
        var accounts = new Mock<ISavingsAccountRepository>(MockBehavior.Strict);
        var before = DateTime.UtcNow;
        transactions.Setup(repository => repository.ProcessPlannedTransactionAsync(8,
                It.Is<DateTime>(cutoff => cutoff.Kind == DateTimeKind.Utc && cutoff >= before), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerEntry { Id = 51, AccountId = 1, Type = "deposit", Amount = 30m });
        var service = new TransactionService(transactions.Object, accounts.Object, NullLogger<TransactionService>.Instance);

        var result = await service.ProcessPlannedTransactionAsync(8);

        Assert.NotNull(result);
        Assert.Equal(51, result.Id);
        Assert.Equal(30m, result.Amount);
        accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_UsesConditionalRepositoryDeletion()
    {
        var transactions = new Mock<ITransactionRepository>(MockBehavior.Strict);
        var accounts = new Mock<ISavingsAccountRepository>(MockBehavior.Strict);
        transactions.Setup(repository => repository.DeleteAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var service = new TransactionService(transactions.Object, accounts.Object, NullLogger<TransactionService>.Instance);

        Assert.True(await service.CancelPlannedAsync(8));

        transactions.Verify(repository => repository.DeleteAsync(8, It.IsAny<CancellationToken>()), Times.Once);
        accounts.VerifyNoOtherCalls();
    }
}
