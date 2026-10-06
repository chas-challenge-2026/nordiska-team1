using Microsoft.Extensions.Options;
using Moq;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;

namespace Nordiska.Modules.Banking.Tests;

public class LoanServiceTests
{
    private const long CustomerId = 1;

    private class FakeLoanRepository : ILoanRepository
    {
        public List<Loan> Loans { get; } = new();
        public List<LedgerEntry> BookedEntries { get; } = new();

        public Task<IEnumerable<Loan>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<Loan>>(Loans.ToList());

        public Task<IEnumerable<Loan>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<Loan>>(Loans.Where(l => l.CustomerId == customerId).ToList());

        public Task<Loan?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Loans.FirstOrDefault(l => l.Id == id));

        public Task CreateWithPayoutAsync(Loan loan, LedgerEntry payout, SavingsAccount account, CancellationToken cancellationToken = default)
        {
            Loans.Add(loan);
            BookedEntries.Add(payout);
            return Task.CompletedTask;
        }

        public Task AddRepaymentAsync(Loan loan, LedgerEntry repayment, SavingsAccount account, CancellationToken cancellationToken = default)
        {
            BookedEntries.Add(repayment);
            return Task.CompletedTask;
        }
    }

    private readonly FakeLoanRepository _loans = new();
    private readonly Mock<ISavingsAccountRepository> _accounts = new();
    private readonly Mock<ITransactionService> _transactions = new();
    private readonly Mock<IPolicyRateService> _policyRate = new();

    public LoanServiceTests()
    {
        _accounts.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsAccount { Id = 1, CustomerId = CustomerId, AccountNumber = "NOR-100001", Status = "active" });
        _accounts.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsAccount { Id = 2, CustomerId = 2, AccountNumber = "NOR-200001", Status = "active" });
        _accounts.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsAccount { Id = 3, CustomerId = CustomerId, AccountNumber = "NOR-100003", Status = "closed" });

        _transactions.Setup(t => t.GetBalanceAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(50000m);

        _policyRate.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PolicyRateResponse(0.0175m, new DateOnly(2026, 9, 18), "Sveriges Riksbank", Stale: false));
    }

    private LoanService CreateService()
        => new(_loans, _accounts.Object, _transactions.Object, _policyRate.Object, Options.Create(new LoanOptions()), new TestLogger<LoanService>());

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // Opened today so no interest has accrued and the amounts in the tests stay exact
    private Loan AddLoan(decimal principal, LoanType type = LoanType.Personal)
    {
        var loan = new Loan(CustomerId, Guid.NewGuid().ToString("N"), type, principal, 0.05m, Today);
        _loans.Loans.Add(loan);
        return loan;
    }

    [Fact]
    public async Task Apply_CreatesActivePersonalLoanAndPaysOut()
    {
        var service = CreateService();

        var loan = await service.ApplyAsync(CustomerId, new ApplyForLoanRequest(100000m, 36, 1));

        Assert.Equal("personal", loan.Type);
        Assert.Equal("active", loan.Status);
        Assert.Equal(100000m, loan.OutstandingAmount);
        Assert.Equal(Today.AddMonths(36), loan.MaturityDate);
        Assert.Equal(32, loan.LoanNumber.Length);

        var payout = Assert.Single(_loans.BookedEntries);
        Assert.Equal(1, payout.AccountId);
        Assert.Equal("deposit", payout.Type);
        Assert.Equal(100000m, payout.Amount);
        Assert.Equal($"Utbetalning lån {loan.LoanNumber}", payout.Label);
    }

    [Fact]
    public async Task Apply_RateIsPolicyRatePlusMargin()
    {
        var service = CreateService();

        var loan = await service.ApplyAsync(CustomerId, new ApplyForLoanRequest(100000m, 36, 1));

        Assert.Equal(0.0675m, loan.InterestRate);
    }

    [Fact]
    public async Task Apply_NoPolicyRate_UsesFallbackBaseRate()
    {
        _policyRate.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((PolicyRateResponse?)null);
        var service = CreateService();

        var loan = await service.ApplyAsync(CustomerId, new ApplyForLoanRequest(100000m, 36, 1));

        Assert.Equal(new LoanOptions().FallbackBaseRate + new LoanOptions().PersonalMargin, loan.InterestRate);
    }

    [Theory]
    [InlineData(9999.99, 36)]
    [InlineData(500000.01, 36)]
    [InlineData(100000, 11)]
    [InlineData(100000, 121)]
    public async Task Apply_OutsideLimits_ThrowsArgumentException(decimal amount, int termMonths)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ApplyAsync(CustomerId, new ApplyForLoanRequest(amount, termMonths, 1)));
        Assert.Empty(_loans.Loans);
    }

    [Fact]
    public async Task Apply_OtherCustomersAccount_ThrowsNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.ApplyAsync(CustomerId, new ApplyForLoanRequest(100000m, 36, 2)));
        Assert.Empty(_loans.Loans);
    }

    [Fact]
    public async Task Apply_ClosedAccount_ThrowsConflict()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.ApplyAsync(CustomerId, new ApplyForLoanRequest(100000m, 36, 3)));
    }

    [Fact]
    public async Task Apply_OverTotalPersonalDebt_ThrowsConflict()
    {
        AddLoan(450000m);
        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.ApplyAsync(CustomerId, new ApplyForLoanRequest(50000.01m, 36, 1)));
        Assert.Single(_loans.Loans);
    }

    [Fact]
    public async Task Apply_MortgageIsNotCountedInDebtCap()
    {
        AddLoan(2000000m, LoanType.Mortgage);
        var service = CreateService();

        var loan = await service.ApplyAsync(CustomerId, new ApplyForLoanRequest(500000m, 36, 1));

        Assert.Equal("active", loan.Status);
    }

    [Fact]
    public async Task Repay_LowersOutstandingAndWithdrawsFromAccount()
    {
        var existing = AddLoan(30000m);
        var service = CreateService();

        var loan = await service.RepayAsync(existing.Id, new RepayLoanRequest(10000m, 1));

        Assert.Equal(20000m, loan.OutstandingAmount);
        Assert.Equal("active", loan.Status);

        var repayment = Assert.Single(_loans.BookedEntries);
        Assert.Equal("withdrawal", repayment.Type);
        Assert.Equal(-10000m, repayment.Amount);
        Assert.Equal($"Amortering lån {existing.LoanNumber}", repayment.Label);
    }

    [Fact]
    public async Task Repay_WholeOutstanding_MarksLoanRepaid()
    {
        var existing = AddLoan(30000m);
        var service = CreateService();

        var loan = await service.RepayAsync(existing.Id, new RepayLoanRequest(30000m, 1));

        Assert.Equal(0m, loan.OutstandingAmount);
        Assert.Equal("repaid", loan.Status);
    }

    [Fact]
    public async Task Repay_MoreThanOutstanding_ThrowsConflict()
    {
        var existing = AddLoan(30000m);
        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.RepayAsync(existing.Id, new RepayLoanRequest(30000.01m, 1)));
        Assert.Empty(_loans.BookedEntries);
    }

    [Fact]
    public async Task Repay_InsufficientFunds_ThrowsConflict()
    {
        var existing = AddLoan(100000m);
        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() => service.RepayAsync(existing.Id, new RepayLoanRequest(50000.01m, 1)));
        Assert.Empty(_loans.BookedEntries);
        Assert.Equal(100000m, existing.OutstandingAmount);
    }

    [Fact]
    public async Task Repay_RepaidLoan_ThrowsConflict()
    {
        var existing = AddLoan(30000m);
        var service = CreateService();
        await service.RepayAsync(existing.Id, new RepayLoanRequest(30000m, 1));

        await Assert.ThrowsAsync<ConflictException>(() => service.RepayAsync(existing.Id, new RepayLoanRequest(1m, 1)));
    }

    [Fact]
    public async Task Repay_FromOtherCustomersAccount_ThrowsNotFound()
    {
        var existing = AddLoan(30000m);
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.RepayAsync(existing.Id, new RepayLoanRequest(1000m, 2)));
    }

    [Fact]
    public async Task GetById_UnknownLoan_ThrowsNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }
}
