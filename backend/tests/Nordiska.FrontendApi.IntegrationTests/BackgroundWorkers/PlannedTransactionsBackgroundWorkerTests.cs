using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nordiska.BuildingBlocks.Database;
using Nordiska.FrontendApi.BackgroundWorkers;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.BackgroundWorkers;

public class PlannedTransactionsBackgroundWorkerTests
{
    private class FakeTxRepo : ITransactionRepository
    {
        public readonly List<LedgerEntry> Store = new();

        public FakeTxRepo(IEnumerable<LedgerEntry> entries)
        {
            Store.AddRange(entries);
        }

        public Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<LedgerEntry>>(Store);

        public Task<PagedResult<LedgerEntry>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
            => Task.FromResult(PagedResult<LedgerEntry>.Create(Store, Store.Count, 1, 50));

        public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.Find(x => x.Id == id));

        public Task<List<LedgerEntry>> GetPendingPlannedTransactionsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            var pending = Store
                .FindAll(l => l.IsPlanned && l.PlannedDate.HasValue && l.PlannedDate.Value <= asOfUtc);
            return Task.FromResult(pending);
        }

        public Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
        {
            entry.Id = Store.Count + 1;
            Store.Add(entry);
            return Task.FromResult(entry.Id);
        }

        public Task<bool> UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
        {
            var idx = Store.FindIndex(l => l.Id == entry.Id);
            if (idx >= 0)
            {
                Store[idx] = entry;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            var idx = Store.FindIndex(l => l.Id == id);
            if (idx >= 0)
            {
                Store.RemoveAt(idx);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    [Fact]
    public async Task ProcessPendingTransactionsAsync_ProcessesDuePlans_AndIgnoresFuturePlans()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var duePlan1 = new LedgerEntry { Id = 1, AccountId = 1, TargetAccountId = 2, Amount = 100m, Type = "transfer", IsPlanned = true, PlannedDate = now.AddMinutes(-10) };
        var duePlan2 = new LedgerEntry { Id = 2, AccountId = 1, TargetAccountId = 2, Amount = 200m, Type = "transfer", IsPlanned = true, PlannedDate = now.AddMinutes(-1) };
        var futurePlan = new LedgerEntry { Id = 3, AccountId = 1, TargetAccountId = 2, Amount = 500m, Type = "transfer", IsPlanned = true, PlannedDate = now.AddDays(1) };

        var repo = new FakeTxRepo(new[] { duePlan1, duePlan2, futurePlan });

        var processedIds = new List<long>();
        var mockTxService = new Mock<ITransactionService>();
        mockTxService
            .Setup(s => s.ProcessPlannedTransactionAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken ct) =>
            {
                processedIds.Add(id);
                return new TransactionResponse(id, 1, "transfer", 100m, DateTime.UtcNow, "Test", 2, false, null, null);
            });

        var services = new ServiceCollection();
        services.AddSingleton<ITransactionRepository>(repo);
        services.AddSingleton<ITransactionService>(mockTxService.Object);
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var configuration = new ConfigurationBuilder().Build();
        var worker = new PlannedTransactionsBackgroundWorker(
            scopeFactory,
            NullLogger<PlannedTransactionsBackgroundWorker>.Instance,
            configuration);

        // Act
        var count = await worker.ProcessPendingTransactionsAsync(CancellationToken.None);

        // Assert
        count.Should().Be(2);
        processedIds.Should().Contain(new[] { 1L, 2L });
        processedIds.Should().NotContain(3L);
    }

    [Fact]
    public async Task ProcessPendingTransactionsAsync_WhenOneFails_ContinuesProcessingRemaining()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var plan1 = new LedgerEntry { Id = 1, AccountId = 1, TargetAccountId = 2, Amount = 100m, Type = "transfer", IsPlanned = true, PlannedDate = now.AddMinutes(-5) };
        var plan2 = new LedgerEntry { Id = 2, AccountId = 1, TargetAccountId = 2, Amount = 200m, Type = "transfer", IsPlanned = true, PlannedDate = now.AddMinutes(-2) };

        var repo = new FakeTxRepo(new[] { plan1, plan2 });

        var processedIds = new List<long>();
        var mockTxService = new Mock<ITransactionService>();
        mockTxService
            .Setup(s => s.ProcessPlannedTransactionAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated unexpected failure"));

        mockTxService
            .Setup(s => s.ProcessPlannedTransactionAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken ct) =>
            {
                processedIds.Add(id);
                return new TransactionResponse(id, 1, "transfer", 200m, DateTime.UtcNow, "Test", 2, false, null, null);
            });

        var services = new ServiceCollection();
        services.AddSingleton<ITransactionRepository>(repo);
        services.AddSingleton<ITransactionService>(mockTxService.Object);
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var configuration = new ConfigurationBuilder().Build();
        var worker = new PlannedTransactionsBackgroundWorker(
            scopeFactory,
            NullLogger<PlannedTransactionsBackgroundWorker>.Instance,
            configuration);

        // Act
        var count = await worker.ProcessPendingTransactionsAsync(CancellationToken.None);

        // Assert: 1 processed successfully despite plan1 throwing
        count.Should().Be(1);
        processedIds.Should().ContainSingle().Which.Should().Be(2L);
    }
}
