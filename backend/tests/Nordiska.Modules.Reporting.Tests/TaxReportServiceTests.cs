using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.BuildingBlocks.Database;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Reporting.Infrastructure;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public class TaxReportServiceTests
{
    private sealed class FakeSavingsAccountService : ISavingsAccountService
    {
        private readonly SavingsAccountResponse? _account;

        public FakeSavingsAccountService(SavingsAccountResponse? account) => _account = account;

        public Task<SavingsAccountResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_account!);

        public Task<IEnumerable<SavingsAccountResponse>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<SavingsAccountResponse>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SavingsAccountResponse> CreateAsync(OpenSavingsAccountRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SavingsAccountResponse> CloseAccountAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeCustomerService : ICustomerService
    {
        private readonly Customer? _customer;

        public FakeCustomerService(Customer? customer) => _customer = customer;

        public Task<Customer> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_customer!);

        public Task<Customer> UpdateAsync(long id, string? name, string? email, string? personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Customer> PatchProfileAsync(long id, string? name, string? email, string? phoneNumber = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Customer> CreateAsync(string name, string email, string personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NotSupportedTransactionService : ITransactionService
    {
        public Task<IEnumerable<TransactionResponse>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PagedResult<TransactionResponse>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TransactionResponse?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TransactionResponse> ExecuteAsync(TransactionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TransactionResponse> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TransactionResponse> CreatePlannedAsync(PlannedTransactionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> CancelPlannedAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<decimal> GetBalanceAsync(long accountId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NotSupportedPdfReportGenerator : IPdfReportGenerator
    {
        public Task<byte[]> GenerateTaxReportPdfAsync(TaxReportData data, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [Fact]
    public async Task CreateJobAsync_AccountNotFound_ThrowsNotFoundException()
    {
        var service = new TaxReportService(
            new FakeSavingsAccountService(null),
            new FakeCustomerService(null),
            new NotSupportedTransactionService(),
            new NotSupportedPdfReportGenerator());

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateJobAsync(customerId: 1, accountId: 42, year: 2025));
    }

    [Fact]
    public async Task GenerateDirectReportAsync_AccountNotFound_ThrowsNotFoundException()
    {
        var service = new TaxReportService(
            new FakeSavingsAccountService(null),
            new FakeCustomerService(null),
            new NotSupportedTransactionService(),
            new NotSupportedPdfReportGenerator());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GenerateDirectReportAsync(customerId: 1, accountId: 42, year: 2025, isAdmin: true));
    }

    [Fact]
    public async Task GenerateDirectReportAsync_CustomerNotFound_ThrowsNotFoundException()
    {
        var account = new SavingsAccountResponse(42, 1, "NOR-1", "Standard", 100m, 0.5m, DateTime.UtcNow);
        var service = new TaxReportService(
            new FakeSavingsAccountService(account),
            new FakeCustomerService(null),
            new NotSupportedTransactionService(),
            new NotSupportedPdfReportGenerator());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GenerateDirectReportAsync(customerId: 1, accountId: 42, year: 2025, isAdmin: true));
    }
}
