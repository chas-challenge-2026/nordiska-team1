using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Mappers;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Infrastructure;

public class LoanService : ILoanService
{
    private readonly ILoanRepository _loanRepo;
    private readonly ISavingsAccountRepository _accountRepo;
    private readonly ITransactionService _transactionService;
    private readonly IPolicyRateService _policyRateService;
    private readonly LoanOptions _options;
    private readonly ILogger<LoanService> _logger;

    public LoanService(
        ILoanRepository loanRepo,
        ISavingsAccountRepository accountRepo,
        ITransactionService transactionService,
        IPolicyRateService policyRateService,
        IOptions<LoanOptions> options,
        ILogger<LoanService> logger)
    {
        _loanRepo = loanRepo;
        _accountRepo = accountRepo;
        _transactionService = transactionService;
        _policyRateService = policyRateService;
        _options = options.Value;
        _logger = logger;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<IEnumerable<LoanResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var loans = await _loanRepo.GetAllAsync(cancellationToken);
        return loans.Select(l => l.ToResponse(Today)).ToList();
    }

    public async Task<IEnumerable<LoanResponse>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var loans = await _loanRepo.GetByCustomerIdAsync(customerId, cancellationToken);
        return loans.Select(l => l.ToResponse(Today)).ToList();
    }

    public async Task<LoanResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var loan = await _loanRepo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Loan with ID {id} was not found.");

        return loan.ToResponse(Today);
    }

    public async Task<LoanResponse> ApplyAsync(long customerId, ApplyForLoanRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount < _options.MinAmount || request.Amount > _options.MaxAmount)
        {
            throw new ArgumentException($"Loan amount must be between {_options.MinAmount:N2} and {_options.MaxAmount:N2} SEK.", nameof(request.Amount));
        }
        if (request.TermMonths < _options.MinTermMonths || request.TermMonths > _options.MaxTermMonths)
        {
            throw new ArgumentException($"Loan term must be between {_options.MinTermMonths} and {_options.MaxTermMonths} months.", nameof(request.TermMonths));
        }

        var account = await GetActiveAccountAsync(request.PayoutAccountId, customerId, cancellationToken);

        var today = Today;
        var existingLoans = await _loanRepo.GetByCustomerIdAsync(customerId, cancellationToken);
        var currentPersonalDebt = existingLoans
            .Where(l => l.Type == LoanType.Personal && l.Status == LoanStatus.Active)
            .Sum(l => l.OutstandingAmount + l.CalculateAccruedInterest(today));

        if (currentPersonalDebt + request.Amount > _options.MaxTotalPersonalDebt)
        {
            throw new ConflictException($"Total personal loan debt can't exceed {_options.MaxTotalPersonalDebt:N2} SEK. Current debt is {currentPersonalDebt:N2}, requested loan is {request.Amount:N2}.");
        }

        var interestRate = await GetBaseRateAsync(cancellationToken) + _options.PersonalMargin;

        var loan = new Loan(
            customerId,
            Guid.NewGuid().ToString("N"),
            LoanType.Personal,
            request.Amount,
            interestRate,
            today,
            today.AddMonths(request.TermMonths));

        var payout = new LedgerEntry
        {
            AccountId = account.Id,
            Type = "deposit",
            Amount = request.Amount,
            Label = $"Utbetalning lån {loan.LoanNumber}",
            CreatedAt = DateTime.UtcNow
        };

        await _loanRepo.CreateWithPayoutAsync(loan, payout, account, cancellationToken);

        _logger.LogInformation("Created loan {Id} (loanNumber={LoanNumber}, amount={Amount}, rate={Rate}, termMonths={TermMonths}) for customer {CustomerId}, paid out to account {AccountId}",
            loan.Id, loan.LoanNumber, loan.PrincipalAmount, loan.InterestRate, request.TermMonths, customerId, account.Id);

        return loan.ToResponse(today);
    }

    public async Task<LoanResponse> RepayAsync(long id, RepayLoanRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Repayment amount must be greater than zero.", nameof(request.Amount));
        }

        var loan = await _loanRepo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Loan with ID {id} was not found.");

        if (loan.Status != LoanStatus.Active)
        {
            throw new ConflictException($"Loan {loan.LoanNumber} is not active and can't be repaid.");
        }

        var account = await GetActiveAccountAsync(request.FromAccountId, loan.CustomerId, cancellationToken);

        // Interest is booked first so the repayment is checked against what is actually owed today
        var today = Today;
        loan.AccrueInterest(today);

        if (request.Amount > loan.OutstandingAmount)
        {
            throw new ConflictException($"Repayment of {request.Amount:N2} exceeds the outstanding amount of {loan.OutstandingAmount:N2} SEK.");
        }

        var balance = await _transactionService.GetBalanceAsync(account.Id, cancellationToken);
        if (balance < request.Amount)
        {
            throw new ConflictException($"Insufficient funds. Current balance is {balance:N2}, requested repayment is {request.Amount:N2}.");
        }

        loan.RegisterRepayment(request.Amount);

        var repayment = new LedgerEntry
        {
            AccountId = account.Id,
            Type = "withdrawal",
            Amount = -request.Amount,
            Label = $"Amortering lån {loan.LoanNumber}",
            CreatedAt = DateTime.UtcNow
        };

        await _loanRepo.AddRepaymentAsync(loan, repayment, account, cancellationToken);

        _logger.LogInformation("Registered repayment of {Amount} on loan {Id} from account {AccountId}, outstanding is now {OutstandingAmount} ({Status})",
            request.Amount, loan.Id, account.Id, loan.OutstandingAmount, loan.Status);

        return loan.ToResponse(today);
    }

    // Another customer's account is reported as not found, so account ids can't be discovered through loans
    private async Task<SavingsAccount> GetActiveAccountAsync(long accountId, long customerId, CancellationToken cancellationToken)
    {
        var account = await _accountRepo.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.CustomerId != customerId)
        {
            throw new NotFoundException($"Account {accountId} not found.");
        }

        if (account.Status != "active")
        {
            throw new ConflictException($"Account {account.AccountNumber} is closed.");
        }

        return account;
    }

    private async Task<decimal> GetBaseRateAsync(CancellationToken cancellationToken)
    {
        var policyRate = await _policyRateService.GetAsync(cancellationToken);
        if (policyRate is not null)
        {
            return policyRate.Rate;
        }

        _logger.LogWarning("No policy rate available, using fallback base rate {Rate} for new loan", _options.FallbackBaseRate);
        return _options.FallbackBaseRate;
    }
}
