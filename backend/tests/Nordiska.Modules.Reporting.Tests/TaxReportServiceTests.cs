using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Reporting.Infrastructure;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public class TaxReportServiceTests
{
    private const long AccountId = 10;

    private readonly Mock<ISavingsAccountService> _accounts = new();
    private readonly Mock<ICustomerService> _customers = new();
    private readonly Mock<ITransactionService> _transactions = new();
    private readonly Mock<IPdfReportGenerator> _pdf = new();
    private TaxReportData? _report;

    public TaxReportServiceTests()
    {
        _customers.Setup(c => c.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => new Customer { Id = id, Name = "Anna Andersson", PersonalNum = "199001011234" });

        // Grab the report data on its way to the pdf generator so the numbers can be checked directly
        _pdf.Setup(p => p.GenerateTaxReportPdfAsync(It.IsAny<TaxReportData>(), It.IsAny<CancellationToken>()))
            .Callback<TaxReportData, CancellationToken>((data, _) => _report = data)
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        SetupAccount(customerId: 1, interestRate: 0.035m);
        SetupTransactions();
    }

    private TaxReportService CreateService() => new(_accounts.Object, _customers.Object, _transactions.Object, _pdf.Object);

    private void SetupAccount(long customerId, decimal interestRate)
    {
        var account = new SavingsAccountResponse(AccountId, customerId, "NOR-123456", "saving", 0m, interestRate, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _accounts.Setup(a => a.GetByIdAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
    }

    private void SetupTransactions(params TransactionResponse[] transactions)
    {
        _transactions.Setup(t => t.QueryAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(transactions);
    }

    private static TransactionResponse Tx(long id, string type, decimal amount, DateTime createdAt) => new(id, AccountId, type, amount, createdAt);

    private static DateTime Utc(int year, int month, int day, int hour = 12) => new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GenerateDirectReport_WithWithdrawal_LowersClosingBalance()
    {
        SetupTransactions(
            Tx(1, "deposit", 1000m, Utc(2025, 2, 1)),
            Tx(2, "withdrawal", -300m, Utc(2025, 6, 15)));

        await CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: false);

        Assert.NotNull(_report);
        Assert.Equal(0m, _report.OpeningBalance);
        Assert.Equal(700m, _report.ClosingBalance);
    }

    [Fact]
    public async Task GenerateDirectReport_WithTransfers_IncludesThemInBalance()
    {
        SetupTransactions(
            Tx(1, "deposit", 1000m, Utc(2025, 1, 10)),
            Tx(2, "transfer", -400m, Utc(2025, 3, 1)),
            Tx(3, "transfer", 150m, Utc(2025, 8, 20)));

        await CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: false);

        Assert.NotNull(_report);
        Assert.Equal(750m, _report.ClosingBalance);
    }

    [Fact]
    public async Task GenerateDirectReport_EntriesBeforeYear_CountAsOpeningBalance()
    {
        SetupTransactions(
            Tx(1, "deposit", 2000m, Utc(2024, 4, 1)),
            Tx(2, "withdrawal", -500m, Utc(2024, 9, 1)),
            Tx(3, "transfer", -250m, Utc(2024, 12, 31, hour: 23)),
            Tx(4, "deposit", 100m, Utc(2025, 5, 1)));

        await CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: false);

        Assert.NotNull(_report);
        Assert.Equal(1250m, _report.OpeningBalance);
        Assert.Equal(1350m, _report.ClosingBalance);
    }

    [Fact]
    public async Task GenerateDirectReport_EntriesAfterYear_AreExcluded()
    {
        SetupTransactions(
            Tx(1, "deposit", 1000m, Utc(2025, 12, 31, hour: 23)),
            Tx(2, "deposit", 5000m, Utc(2026, 1, 1, hour: 0)),
            Tx(3, "withdrawal", -200m, Utc(2026, 2, 1)));

        await CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: false);

        Assert.NotNull(_report);
        Assert.Equal(1000m, _report.ClosingBalance);
    }

    [Fact]
    public async Task GenerateDirectReport_CalculatesInterestAndTaxFromClosingBalance()
    {
        SetupTransactions(Tx(1, "deposit", 10000m, Utc(2025, 1, 5)));

        await CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: false);

        Assert.NotNull(_report);
        Assert.Equal(0.035m, _report.InterestRate);
        Assert.Equal(350m, _report.TotalInterestEarned);
        Assert.Equal(105m, _report.TotalTaxWithheld);
    }

    [Fact]
    public async Task GenerateDirectReport_RoundsInterestAndTaxToTwoDecimals()
    {
        SetupAccount(customerId: 1, interestRate: 0.031m);
        SetupTransactions(Tx(1, "deposit", 1234.56m, Utc(2025, 1, 5)));

        await CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: false);

        // 1234.56 * 0.031 = 38.27136 and 38.27 * 0.30 = 11.481
        Assert.NotNull(_report);
        Assert.Equal(38.27m, _report.TotalInterestEarned);
        Assert.Equal(11.48m, _report.TotalTaxWithheld);
    }

    [Fact]
    public async Task GenerateDirectReport_EmptyAccount_GivesNoInterest()
    {
        await CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: false);

        Assert.NotNull(_report);
        Assert.Equal(0m, _report.ClosingBalance);
        Assert.Equal(0m, _report.TotalInterestEarned);
        Assert.Equal(0m, _report.TotalTaxWithheld);
    }

    [Fact]
    public async Task GenerateDirectReport_OtherCustomersAccount_ThrowsAuthenticationException()
    {
        SetupAccount(customerId: 2, interestRate: 0.035m);

        await Assert.ThrowsAsync<AuthenticationException>(() => CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: false));

        _pdf.Verify(p => p.GenerateTaxReportPdfAsync(It.IsAny<TaxReportData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateDirectReport_AsAdmin_CanReportOnAnyAccount()
    {
        SetupAccount(customerId: 2, interestRate: 0.035m);

        var (fileBytes, fileName) = await CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: true);

        Assert.NotEmpty(fileBytes);
        Assert.Equal("skatteunderlag_2025_NOR-123456.pdf", fileName);
        Assert.NotNull(_report);
        Assert.Equal(2, _report.CustomerId);
    }

    [Fact]
    public async Task GenerateDirectReport_AccountNotFound_ThrowsAndGeneratesNothing()
    {
        _accounts.Setup(a => a.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Savings account with ID {AccountId} was not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GenerateDirectReportAsync(1, AccountId, 2025, isAdmin: false));

        _pdf.Verify(p => p.GenerateTaxReportPdfAsync(It.IsAny<TaxReportData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateJobAsync_AccountNotFound_ThrowsNotFoundException()
    {
        _accounts.Setup(a => a.GetByIdAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync((SavingsAccountResponse)null!);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().CreateJobAsync(customerId: 1, accountId: AccountId, year: 2025));
    }

    [Fact]
    public async Task GenerateDirectReportAsync_AccountNotFound_ThrowsNotFoundException()
    {
        _accounts.Setup(a => a.GetByIdAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync((SavingsAccountResponse)null!);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GenerateDirectReportAsync(customerId: 1, accountId: AccountId, year: 2025, isAdmin: true));
    }

    [Fact]
    public async Task GenerateDirectReportAsync_CustomerNotFound_ThrowsNotFoundException()
    {
        _customers.Setup(c => c.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((Customer)null!);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GenerateDirectReportAsync(customerId: 1, accountId: AccountId, year: 2025, isAdmin: true));
    }
}
