using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Infrastructure;

namespace Nordiska.Modules.Banking.Tests;

public sealed class SavingsGoalDepositTests
{
    [Fact]
    public async Task DepositAsync_WithZeroAmount_RejectsRequestBeforeRepositoryCall()
    {
        var depositRepository = new Mock<ISavingsGoalDepositRepository>();
        var service = CreateService(depositRepository);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.DepositAsync(
                10,
                new DepositToSavingsGoalRequest(2, 0m),
                1));

        depositRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DepositAsync_ReturnsAtomicRepositoryResult()
    {
        var depositedAt = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var depositRepository = new Mock<ISavingsGoalDepositRepository>();
        depositRepository
            .Setup(repository => repository.DepositAsync(
                10,
                2,
                600m,
                1,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsGoalDepositResult(
                10,
                1,
                "Semester",
                2,
                1,
                600m,
                4400m,
                1600m,
                1100m,
                1000m,
                "completed",
                true,
                depositedAt));

        var service = CreateService(depositRepository);

        var result = await service.DepositAsync(
            10,
            new DepositToSavingsGoalRequest(2, 600m),
            1);

        Assert.Equal(10, result.SavingsGoalId);
        Assert.Equal(1100m, result.CurrentAmount);
        Assert.Equal("completed", result.Status);
        Assert.True(result.CompletedNow);
        Assert.Equal(depositedAt, result.DepositedAt);
    }

    private static SavingsGoalService CreateService(
        Mock<ISavingsGoalDepositRepository> depositRepository)
    {
        return new SavingsGoalService(
            new Mock<ISavingsGoalRepository>().Object,
            new Mock<ISavingsAccountRepository>().Object,
            new Mock<ITransactionRepository>().Object,
            NullLogger<SavingsGoalService>.Instance,
            new Mock<IInterestRateService>().Object,
            depositRepository.Object);
    }
}
