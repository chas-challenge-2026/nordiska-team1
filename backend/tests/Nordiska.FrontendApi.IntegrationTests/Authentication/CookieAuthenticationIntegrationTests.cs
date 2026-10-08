using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Nordiska.BuildingBlocks.Database;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.FrontendApi.Authentication;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.Contracts.Responses;
using Nordiska.FrontendApi.Extensions;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.Inbox.Application;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Authentication;

public class TestAuthService : IAuthService
{
    private readonly IJwtProvider _jwtProvider;
    private static readonly ConcurrentDictionary<string, Customer> _customers = new();
    private static readonly ConcurrentDictionary<string, string> _orders = new();

    static TestAuthService()
    {
        var anna = new Customer
        {
            Id = 101,
            Name = "Anna Smith",
            Email = "anna@exempel.se",
            PersonalNum = "198202116050",
            PhoneNumber = "+46701112233"
        };
        var erik = new Customer
        {
            Id = 102,
            Name = "Erik Svensson",
            Email = "erik@exempel.se",
            PersonalNum = "197903142380",
            PhoneNumber = "+46702223344"
        };

        _customers[anna.PersonalNum] = anna;
        _customers[anna.Email] = anna;
        _customers["anna@example.com"] = anna;
        _customers[erik.PersonalNum] = erik;
        _customers[erik.Email] = erik;
        _customers["erik@example.com"] = erik;
    }

    public TestAuthService(IJwtProvider jwtProvider)
    {
        _jwtProvider = jwtProvider;
    }

    public Task<AuthenticationResultDto> InitiateBankIdAsync(BankIdInitiateRequest request, string clientIp)
    {
        var cleanPersonalNum = request.PersonalNum?.Replace("-", "").Trim() ?? "198202116050";
        var orderRef = Guid.NewGuid().ToString();
        _orders[orderRef] = cleanPersonalNum;

        var dto = new BankIdInitiateResponseDto(orderRef, "test-auto-start-token", "test-qr-code", "test-qr-secret");
        return Task.FromResult(new AuthenticationResultDto(true, null, InitiateData: dto));
    }

    public async Task<AuthenticationResultDto> CollectBankIdAsync(BankIdCollectRequest request, HttpResponse response)
    {
        if (!_orders.TryGetValue(request.OrderRef, out var personalNum))
        {
            personalNum = "198202116050";
        }

        if (!_customers.TryGetValue(personalNum, out var customer))
        {
            customer = new Customer
            {
                Id = Random.Shared.Next(100, 9999),
                Name = $"BankID User {personalNum}",
                Email = $"user_{personalNum}@nordiska.se",
                PersonalNum = personalNum,
                PhoneNumber = "+46700000000"
            };
            _customers[personalNum] = customer;
            _customers[customer.Email] = customer;
        }

        var token = await _jwtProvider.Generate(customer);
        response.AppendAuthCookie(token, 15);

        var completeData = new BankIdCollectResponseDto(
            "COMPLETE",
            null,
            new CustomerResponseDto(customer.Id, customer.Email ?? string.Empty, customer.Name)
        );

        return new AuthenticationResultDto(true, null, Token: token, CollectData: completeData);
    }

    public async Task<AuthenticationResultDto> RegisterCustomerAsync(RegisterCustomerRequestDto request, HttpResponse response)
    {
        var cleanEmail = request.Email?.Trim() ?? string.Empty;
        if (_customers.ContainsKey(cleanEmail))
        {
            return new AuthenticationResultDto(
                IsSuccess: false,
                ErrorMessage: "En användare med denna e-post finns redan.",
                FailureReason: AuthFailureReason.Conflict,
                ConflictCode: "EMAIL_TAKEN");
        }

        var cleanPersonalNum = request.PersonalNum.Replace("-", "").Trim();
        if (_customers.ContainsKey(cleanPersonalNum))
        {
            return new AuthenticationResultDto(
                IsSuccess: false,
                ErrorMessage: "En användare med detta personnummer finns redan.",
                FailureReason: AuthFailureReason.Conflict,
                ConflictCode: "PERSONAL_NUM_TAKEN");
        }

        var customer = new Customer
        {
            Id = Random.Shared.Next(100, 9999),
            Name = request.Name,
            Email = cleanEmail,
            PersonalNum = cleanPersonalNum,
            PhoneNumber = request.PhoneNumber
        };

        _customers[cleanPersonalNum] = customer;
        _customers[cleanEmail] = customer;

        var token = await _jwtProvider.Generate(customer);
        response.AppendAuthCookie(token, 15);

        var customerDto = new CustomerResponseDto(customer.Id, customer.Email, customer.Name, token);

        return new AuthenticationResultDto(
            IsSuccess: true,
            ErrorMessage: null,
            Token: token,
            Customer: customerDto);
    }

    public async Task<AuthenticationResultDto> LoginAsync(LoginRequest request, HttpResponse response)
    {
        if (_customers.TryGetValue(request.Email, out var customer))
        {
            var token = await _jwtProvider.Generate(customer);
            response.AppendAuthCookie(token, 15);
            var completeData = new BankIdCollectResponseDto(
                "COMPLETE",
                null,
                new CustomerResponseDto(customer.Id, customer.Email ?? string.Empty, customer.Name)
            );
            return new AuthenticationResultDto(true, null, Token: token, CollectData: completeData);
        }

        return new AuthenticationResultDto(false, "Ogiltig e-postadress eller lösenord.");
    }

    public async Task<AuthenticationResultDto> RefreshSessionAsync(string customerIdOrEmail, HttpResponse response)
    {
        Customer? customer = null;
        if (long.TryParse(customerIdOrEmail, out var id))
        {
            customer = _customers.Values.FirstOrDefault(c => c.Id == id);
        }

        if (customer == null && _customers.TryGetValue(customerIdOrEmail, out var c))
        {
            customer = c;
        }

        if (customer != null)
        {
            var token = await _jwtProvider.Generate(customer);
            response.AppendAuthCookie(token, 15);
            var completeData = new BankIdCollectResponseDto(
                "COMPLETE",
                null,
                new CustomerResponseDto(customer.Id, customer.Email ?? string.Empty, customer.Name)
            );
            return new AuthenticationResultDto(true, null, Token: token, CollectData: completeData);
        }

        return new AuthenticationResultDto(false, "Användaren hittades inte.");
    }
}

public class TestSavingsAccountRepository : ISavingsAccountRepository
{
    private static readonly List<SavingsAccount> _store = new()
    {
        new SavingsAccount { Id = 1, CustomerId = 101, AccountNumber = "NOR-100001", AccountType = "saving", AccountName = "Sparkonto", Balance = 5000m, InterestRate = 0.025m, CreatedAt = DateTime.UtcNow },
        new SavingsAccount { Id = 2, CustomerId = 101, AccountNumber = "NOR-100002", AccountType = "checking", AccountName = "Lönekonto", Balance = 10000m, InterestRate = 0.005m, CreatedAt = DateTime.UtcNow },
        new SavingsAccount { Id = 3, CustomerId = 102, AccountNumber = "NOR-200001", AccountType = "saving", AccountName = "Eriks Spar", Balance = 3000m, InterestRate = 0.025m, CreatedAt = DateTime.UtcNow },
    };
    private static long _next = 10;

    public Task<IEnumerable<SavingsAccount>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<SavingsAccount>>(_store.ToList());

    public Task<IEnumerable<SavingsAccount>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<SavingsAccount>>(_store.Where(s => s.CustomerId == customerId).ToList());

    public Task<SavingsAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.FirstOrDefault(s => s.Id == id));

    public Task<int> CountFavoritesByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Count(s => s.CustomerId == customerId && s.IsFavorite && s.Status == "active"));

    public Task<long> CreateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
    {
        entity.Id = _next++;
        _store.Add(entity);
        return Task.FromResult(entity.Id);
    }

    public Task UpdateAsync(SavingsAccount entity, CancellationToken cancellationToken = default)
    {
        var idx = _store.FindIndex(s => s.Id == entity.Id);
        if (idx >= 0) _store[idx] = entity;
        return Task.CompletedTask;
    }
}

public class TestTransactionRepository : ITransactionRepository
{
    private static readonly List<LedgerEntry> _store = new()
    {
        new LedgerEntry { Id = 1, AccountId = 1, Type = "deposit", Amount = 5000m, CreatedAt = DateTime.UtcNow.AddDays(-10), Label = "Insättning" },
        new LedgerEntry { Id = 2, AccountId = 2, Type = "deposit", Amount = 10000m, CreatedAt = DateTime.UtcNow.AddDays(-5), Label = "Lön" }
    };
    private static long _next = 10;

    public Task<IEnumerable<LedgerEntry>> QueryAsync(long? accountId = null, CancellationToken cancellationToken = default)
    {
        var q = _store.Where(l => !l.IsPlanned).AsEnumerable();
        if (accountId.HasValue) q = q.Where(l => l.AccountId == accountId.Value);
        return Task.FromResult<IEnumerable<LedgerEntry>>(q.ToList());
    }

    public Task<PagedResult<LedgerEntry>> QueryPagedAsync(TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var q = _store.AsEnumerable();

        if (parameters.AccountIds != null && parameters.AccountIds.Count > 0)
        {
            q = q.Where(l => parameters.AccountIds.Contains(l.AccountId));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Type))
        {
            var typeLower = parameters.Type.Trim().ToLowerInvariant();
            q = q.Where(l => l.Type.ToLowerInvariant() == typeLower);
        }

        if (parameters.FromDate.HasValue)
            q = q.Where(l => l.CreatedAt >= parameters.FromDate.Value);

        if (parameters.ToDate.HasValue)
            q = q.Where(l => l.CreatedAt <= parameters.ToDate.Value);

        if (parameters.MinAmount.HasValue)
            q = q.Where(l => l.Amount >= parameters.MinAmount.Value);

        if (parameters.MaxAmount.HasValue)
            q = q.Where(l => l.Amount <= parameters.MaxAmount.Value);

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim().ToLowerInvariant();
            q = q.Where(l => l.Type.ToLowerInvariant().Contains(term)
                             || (l.Label != null && l.Label.ToLowerInvariant().Contains(term))
                             || l.Id.ToString().Contains(term)
                             || l.AccountId.ToString().Contains(term));
        }

        var totalCount = q.Count();
        var page = parameters.NormalizedPage;
        var pageSize = parameters.NormalizedPageSize;
        var isAsc = string.Equals(parameters.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        q = isAsc ? q.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id) : q.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id);

        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(PagedResult<LedgerEntry>.Create(items, totalCount, page, pageSize));
    }

    public Task<LedgerEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.FirstOrDefault(l => l.Id == id));

    public Task<List<LedgerEntry>> GetPendingPlannedTransactionsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var pending = _store
            .Where(l => l.IsPlanned && l.PlannedDate.HasValue && l.PlannedDate.Value <= asOfUtc)
            .OrderBy(l => l.PlannedDate)
            .ThenBy(l => l.Id)
            .ToList();
        return Task.FromResult(pending);
    }

    public Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        entry.Id = _next++;
        _store.Add(entry);
        return Task.FromResult(entry.Id);
    }

    public Task<bool> UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        var idx = _store.FindIndex(l => l.Id == entry.Id);
        if (idx >= 0)
        {
            _store[idx] = entry;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var idx = _store.FindIndex(l => l.Id == id);
        if (idx >= 0)
        {
            _store.RemoveAt(idx);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<LedgerEntry?> GetPlannedTransactionByGoalIdAsync(long savingsGoalId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.FirstOrDefault(l => l.SavingsGoalId == savingsGoalId && l.IsPlanned));
}

public class TestSavingsGoalRepository : ISavingsGoalRepository
{
    private static readonly List<SavingsGoal> _store = new();
    private static long _next = 1;

    public static void Reset()
    {
        _store.Clear();
        _next = 1;
    }

    public static void Seed(SavingsGoal goal)
    {
        if (goal.Id == 0) goal.Id = _next++;
        _store.Add(goal);
    }

    public Task<SavingsGoal?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.FirstOrDefault(g => g.Id == id));

    public Task<List<SavingsGoal>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Where(g => g.CustomerId == customerId).ToList());

    public Task<List<SavingsGoal>> GetByAccountIdAsync(long accountId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Where(g => g.AccountId == accountId).ToList());

    public Task<long> CreateAsync(SavingsGoal goal, CancellationToken cancellationToken = default)
    {
        goal.Id = _next++;
        _store.Add(goal);
        return Task.FromResult(goal.Id);
    }

    public Task<bool> UpdateAsync(SavingsGoal goal, CancellationToken cancellationToken = default)
    {
        var idx = _store.FindIndex(g => g.Id == goal.Id);
        if (idx >= 0)
        {
            _store[idx] = goal;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var count = _store.RemoveAll(g => g.Id == id);
        return Task.FromResult(count > 0);
    }
}

public sealed class TestSavingsGoalDepositRepository(
    ISavingsGoalRepository goalRepository,
    ISavingsAccountRepository accountRepository)
    : ISavingsGoalDepositRepository
{
    public async Task<SavingsGoalDepositResult> DepositAsync(
        long savingsGoalId,
        long sourceAccountId,
        decimal amount,
        long customerId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var goal = await goalRepository.GetByIdAsync(savingsGoalId, cancellationToken)
            ?? throw new NotFoundException($"Sparmål med ID {savingsGoalId} hittades inte.");

        var sourceAccount = await accountRepository.GetByIdAsync(sourceAccountId, cancellationToken)
            ?? throw new NotFoundException($"Källkonto med ID {sourceAccountId} hittades inte.");

        var targetAccount = await accountRepository.GetByIdAsync(goal.AccountId, cancellationToken)
            ?? throw new NotFoundException($"Sparmålets konto med ID {goal.AccountId} hittades inte.");

        if (!isAdmin && goal.CustomerId != customerId)
        {
            throw new NotFoundException($"Sparmål med ID {savingsGoalId} hittades inte.");
        }

        if (!isAdmin && sourceAccount.CustomerId != customerId)
        {
            throw new ValidationException("Källkontot tillhör inte den inloggade kunden.");
        }

        if (sourceAccount.Balance < amount)
        {
            throw new ConflictException("Otillräckligt saldo.");
        }

        var depositedAt = DateTime.UtcNow;
        goal.CurrentAmount += amount;
        goal.UpdatedAt = depositedAt;

        var completedNow = goal.CurrentAmount >= goal.TargetAmount &&
                           !string.Equals(goal.Status, "completed", StringComparison.OrdinalIgnoreCase);

        if (completedNow)
        {
            goal.Status = "completed";
        }

        await goalRepository.UpdateAsync(goal, cancellationToken);

        return new SavingsGoalDepositResult(
            goal.Id,
            goal.CustomerId,
            goal.Title,
            sourceAccount.Id,
            targetAccount.Id,
            amount,
            sourceAccount.Balance - amount,
            targetAccount.Balance + amount,
            goal.CurrentAmount,
            goal.TargetAmount,
            goal.Status,
            completedNow,
            depositedAt);
    }
}

public class TestCustomerService : ICustomerService
{
    private static readonly ConcurrentDictionary<long, Customer> _customers = new();

    static TestCustomerService()
    {
        var anna = new Customer { Id = 101, Name = "Anna Smith", Email = "anna@exempel.se", PersonalNum = "198202116050", PhoneNumber = "+46701112233", CreatedAt = DateTime.UtcNow };
        var erik = new Customer { Id = 102, Name = "Erik Svensson", Email = "erik@exempel.se", PersonalNum = "197903142380", PhoneNumber = "+46702223344", CreatedAt = DateTime.UtcNow };
        _customers[101] = anna;
        _customers[102] = erik;
    }

    public Task<Customer> CreateAsync(string name, string email, string personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default)
    {
        var id = Random.Shared.Next(100, 9999);
        var c = new Customer { Id = id, Name = name, Email = email, PersonalNum = personalNum, PhoneNumber = phoneNumber, CreatedAt = DateTime.UtcNow };
        _customers[id] = c;
        return Task.FromResult(c);
    }

    public Task<Customer> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        if (_customers.TryGetValue(id, out var customer))
            return Task.FromResult(customer);
        throw new NotFoundException($"Customer with id {id} was not found.");
    }

    public Task<Customer> UpdateAsync(long id, string? name, string? email, string? personalNum, string? phoneNumber = null, List<string>? overviewPreference = null, CancellationToken cancellationToken = default)
    {
        var customer = _customers.GetOrAdd(id, k => new Customer { Id = k, Name = "User", Email = "u@ex.se", PersonalNum = "198001010000" });
        if (!string.IsNullOrWhiteSpace(name)) customer.Name = name;
        if (!string.IsNullOrWhiteSpace(email)) customer.Email = email;
        if (!string.IsNullOrWhiteSpace(personalNum)) customer.PersonalNum = personalNum;
        if (phoneNumber != null) customer.PhoneNumber = phoneNumber;
        if (overviewPreference != null) customer.OverviewPreference = overviewPreference;
        customer.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(customer);
    }

    public Task<Customer> PatchProfileAsync(long id, string? name, string? email, string? phoneNumber = null, List<string>? overviewPreference = null, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(id, name, email, null, phoneNumber, overviewPreference, cancellationToken);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        _customers.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public class TestAccountTypeConfigRepository : IAccountTypeConfigRepository
{
    private static readonly List<AccountTypeConfig> _configs = new()
    {
        new AccountTypeConfig { AccountType = "flex", InterestRate = 0.0350m, Description = "Flexible savings account with variable interest rate." },
        new AccountTypeConfig { AccountType = "fix", InterestRate = 0.0410m, Description = "Fixed-term savings account with 3-month lock-in." },
        new AccountTypeConfig { AccountType = "standard", InterestRate = 0.0250m, Description = "Standard savings account for everyday savings." },
        new AccountTypeConfig { AccountType = "saving", InterestRate = 0.0350m, Description = "High-yield savings account." },
        new AccountTypeConfig { AccountType = "premium", InterestRate = 0.0400m, Description = "Premium savings account with top-tier interest rate." }
    };

    public Task<IEnumerable<AccountTypeConfig>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<AccountTypeConfig>>(_configs);

    public Task<AccountTypeConfig?> GetByTypeAsync(string accountType, CancellationToken cancellationToken = default)
        => Task.FromResult(_configs.FirstOrDefault(c => string.Equals(c.AccountType, accountType, StringComparison.OrdinalIgnoreCase)));

    public Task CreateAsync(AccountTypeConfig entity, CancellationToken cancellationToken = default)
    {
        _configs.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AccountTypeConfig entity, CancellationToken cancellationToken = default)
    {
        var existing = _configs.FirstOrDefault(c => string.Equals(c.AccountType, entity.AccountType, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.InterestRate = entity.InterestRate;
            existing.Description = entity.Description;
        }
        return Task.CompletedTask;
    }

    private static readonly List<AccountTypeRateHistory> _rateHistories = new();

    public Task<IEnumerable<AccountTypeRateHistory>> GetRateHistoryAsync(string accountType, CancellationToken cancellationToken = default)
    {
        var list = _rateHistories
            .Where(r => string.Equals(r.AccountType, accountType, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.EffectiveFromUtc)
            .ToList();
        return Task.FromResult<IEnumerable<AccountTypeRateHistory>>(list);
    }

    public Task AddRateHistoryAsync(AccountTypeRateHistory history, CancellationToken cancellationToken = default)
    {
        _rateHistories.Add(history);
        return Task.CompletedTask;
    }
}

public class TestOperationalMessageRepository : IOperationalMessageRepository
{
    private static readonly List<OperationalMessage> _store = new()
    {
        new OperationalMessage
        {
            Id = 1,
            TitleSv = "Planerat driftunderhåll",
            TitleEn = "Scheduled maintenance",
            MessageSv = "Underhåll utförs i helgen.",
            MessageEn = "Maintenance during weekend.",
            Severity = "warning",
            Priority = 10,
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };
    private static long _next = 10;

    public Task<IEnumerable<OperationalMessage>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var active = _store
            .Where(m => m.IsActive
                        && (m.StartDate == null || m.StartDate <= now)
                        && (m.EndDate == null || m.EndDate >= now))
            .OrderByDescending(m => m.Priority)
            .ThenByDescending(m => m.CreatedAt)
            .ToList();
        return Task.FromResult<IEnumerable<OperationalMessage>>(active);
    }

    public Task<IEnumerable<OperationalMessage>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var all = _store
            .OrderByDescending(m => m.Priority)
            .ThenByDescending(m => m.CreatedAt)
            .ToList();
        return Task.FromResult<IEnumerable<OperationalMessage>>(all);
    }

    public Task<OperationalMessage?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var match = _store.FirstOrDefault(m => m.Id == id);
        return Task.FromResult(match);
    }

    public Task CreateAsync(OperationalMessage entity, CancellationToken cancellationToken = default)
    {
        entity.Id = _next++;
        _store.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(OperationalMessage entity, CancellationToken cancellationToken = default)
    {
        var idx = _store.FindIndex(m => m.Id == entity.Id);
        if (idx >= 0) _store[idx] = entity;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var count = _store.RemoveAll(m => m.Id == id);
        return Task.FromResult(count > 0);
    }
}

// Anna (customer 1) has loan 1, Erik (customer 2) has loan 2
public class TestLoanRepository : ILoanRepository
{
    private static readonly List<Loan> _store = new()
    {
        CreateLoan(1, 101, 50000m),
        CreateLoan(2, 102, 80000m)
    };

    // Id has a private setter since EF is the one that normally sets it
    private static Loan CreateLoan(long id, long customerId, decimal principal)
    {
        var loan = new Loan(customerId, $"LN-TEST-{id}", LoanType.Personal, principal, 0.0675m, DateOnly.FromDateTime(DateTime.UtcNow));
        typeof(Loan).GetProperty(nameof(Loan.Id))!.SetValue(loan, id);
        return loan;
    }

    public Task<IEnumerable<Loan>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Loan>>(_store.ToList());

    public Task<IEnumerable<Loan>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Loan>>(_store.Where(l => l.CustomerId == customerId).ToList());

    public Task<Loan?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.FirstOrDefault(l => l.Id == id));

    public Task CreateWithPayoutAsync(Loan loan, LedgerEntry payout, SavingsAccount account, CancellationToken cancellationToken = default)
    {
        _store.Add(loan);
        return Task.CompletedTask;
    }

    public Task AddRepaymentAsync(Loan loan, LedgerEntry repayment, SavingsAccount account, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public class CustomAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public ConcurrentQueue<(long CustomerId, string Type, string Title, long? TargetId)> Notifications { get; } = new();
    public Exception? NotificationFailure { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());

        // Tests share one factory per class, so raise the limits to keep rate limiting out of the way (NOR-70)
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=test;Username=postgres;Password=postgres");
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "10000");
        builder.UseSetting("RateLimiting:Transactions:PermitLimit", "10000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddScoped<IAuthService, TestAuthService>();

            services.RemoveAll<ISavingsAccountRepository>();
            services.AddScoped<ISavingsAccountRepository, TestSavingsAccountRepository>();

            services.RemoveAll<ITransactionRepository>();
            services.AddScoped<ITransactionRepository, TestTransactionRepository>();

            services.RemoveAll<ICustomerService>();
            services.AddScoped<ICustomerService, TestCustomerService>();

            services.RemoveAll<IAccountTypeConfigRepository>();
            services.AddScoped<IAccountTypeConfigRepository, TestAccountTypeConfigRepository>();

            services.RemoveAll<IOperationalMessageRepository>();
            services.AddScoped<IOperationalMessageRepository, TestOperationalMessageRepository>();

            services.RemoveAll<ILoanRepository>();
            services.AddScoped<ILoanRepository, TestLoanRepository>();

            services.RemoveAll<ISavingsGoalRepository>();
            services.AddScoped<ISavingsGoalRepository, TestSavingsGoalRepository>();

            services.RemoveAll<ISavingsGoalDepositRepository>();
            services.AddScoped<ISavingsGoalDepositRepository, TestSavingsGoalDepositRepository>();

            var inboxRepository = new Mock<IInboxRepository>();
            inboxRepository
                .Setup(repository => repository.AddNotificationAsync(
                    It.IsAny<long>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<NotificationPriority>(),
                    It.IsAny<NotificationTargetType?>(),
                    It.IsAny<long?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<long, string, string, string?, NotificationPriority, NotificationTargetType?, long?, CancellationToken>(
                    (customerId, type, title, _, _, _, targetId, _) =>
                        Notifications.Enqueue((customerId, type, title, targetId)))
                .Returns<long, string, string, string?, NotificationPriority, NotificationTargetType?, long?, CancellationToken>(
                    (_, _, _, _, _, _, _, _) => NotificationFailure is null
                        ? Task.FromResult(new CustomerNotification(1, "test", "test"))
                        : Task.FromException<CustomerNotification>(NotificationFailure));

            services.RemoveAll<IInboxRepository>();
            services.AddSingleton(inboxRepository.Object);
        });
    }
}

public class CookieAuthenticationIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public CookieAuthenticationIntegrationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BankIdCollect_WithValidCustomer_Sets_HttpOnly_AuthCookie()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

        // 1. Initiate BankID with seeded user's personal number
        var initiateResponse = await client.PostAsJsonAsync("/api/auth/bankid/initiate", new
        {
            personalNum = "198202116050"
        });
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initiateContent = await initiateResponse.Content.ReadAsStringAsync();
        using var initDoc = JsonDocument.Parse(initiateContent);
        var orderRef = initDoc.RootElement.GetProperty("orderRef").GetString();

        // 2. Collect BankID (polling until COMPLETE in simulated flow)
        HttpResponseMessage collectResponse = null!;
        for (var i = 0; i < 15; i++)
        {
            collectResponse = await client.PostAsJsonAsync("/api/auth/bankid/collect", new
            {
                orderRef = orderRef
            });

            var content = await collectResponse.Content.ReadAsStringAsync();
            if (!collectResponse.IsSuccessStatusCode)
            {
                break;
            }

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("status", out var s) || doc.RootElement.TryGetProperty("Status", out s))
            {
                if (string.Equals(s.GetString(), "COMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            await Task.Delay(200);
        }

        // Assert
        var failureBody = await collectResponse.Content.ReadAsStringAsync();
        collectResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: failureBody);
        collectResponse.Headers.Contains("Set-Cookie").Should().BeTrue();

        var setCookieHeader = collectResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
        setCookieHeader.Should().NotBeNull();
        setCookieHeader.Should().Contain("access_token=");
        setCookieHeader.Should().Contain("httponly");
        setCookieHeader.Should().Contain("path=/");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAuthCookie_Returns_Ok_And_UserInfo()
    {
        // Arrange: Use cookie-handling HttpClient to simulate a browser session
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        // Act 1: Initiate & Collect BankID with Erik's personal number
        var initiateResponse = await client.PostAsJsonAsync("/api/auth/bankid/initiate", new
        {
            personalNum = "197903142380"
        });
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initiateContent = await initiateResponse.Content.ReadAsStringAsync();
        using var initDoc = JsonDocument.Parse(initiateContent);
        var orderRef = initDoc.RootElement.GetProperty("orderRef").GetString();

        HttpResponseMessage collectResponse = null!;
        for (var i = 0; i < 15; i++)
        {
            collectResponse = await client.PostAsJsonAsync("/api/auth/bankid/collect", new
            {
                orderRef = orderRef
            });

            var content = await collectResponse.Content.ReadAsStringAsync();
            if (!collectResponse.IsSuccessStatusCode)
            {
                break;
            }

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("status", out var s) || doc.RootElement.TryGetProperty("Status", out s))
            {
                if (string.Equals(s.GetString(), "COMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            await Task.Delay(200);
        }
        var failureBody = await collectResponse.Content.ReadAsStringAsync();
        collectResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: failureBody);

        // Act 2: Access protected /api/auth/me endpoint using the automatically attached cookie
        var meResponse = await client.GetAsync("/api/auth/me");

        // Assert
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meContent = await meResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(meContent);
        var email = jsonDoc.RootElement.GetProperty("email").GetString();
        email.Should().Be("erik@exempel.se");
    }

    [Fact]
    public async Task Register_WithValidCustomer_Sets_HttpOnly_AuthCookie()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var validPersonalNum = CreateRandomPersonalNum();
        var registerPayload = new
        {
            name = $"Test Person {uniqueId}",
            personalNum = validPersonalNum,
            email = $"test_{uniqueId}@example.com",
            phoneNumber = "+46701234567"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", registerPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").Should().BeTrue();

        var setCookieHeader = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        setCookieHeader.Should().NotBeNull();
        setCookieHeader.Should().Contain("access_token=");
        setCookieHeader.Should().Contain("httponly");
        setCookieHeader.Should().Contain("path=/");

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        jsonDoc.RootElement.GetProperty("name").GetString().Should().Be($"Test Person {uniqueId}");
        jsonDoc.RootElement.GetProperty("email").GetString().Should().Be($"test_{uniqueId}@example.com");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns_409Conflict_With_EmailTakenCode()
    {
        // Arrange: Anna is already a seeded customer with anna@exempel.se
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var payload = new
        {
            name = "Another Anna",
            personalNum = CreateRandomPersonalNum(),
            email = "anna@exempel.se", // Already exists
            phoneNumber = "+46701112233"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        jsonDoc.RootElement.GetProperty("status").GetInt32().Should().Be(409);
        jsonDoc.RootElement.GetProperty("code").GetString().Should().Be("EMAIL_TAKEN");
    }

    [Fact]
    public async Task Register_DuplicatePersonalNum_Returns_409Conflict_With_PersonalNumTakenCode()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var sharedPersonalNum = CreateRandomPersonalNum();
        var uniqueId1 = Guid.NewGuid().ToString("N")[..8];
        var firstCustomer = new
        {
            name = $"First User {uniqueId1}",
            personalNum = sharedPersonalNum,
            email = $"first_{uniqueId1}@example.com",
            phoneNumber = "+46701112233"
        };

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", firstCustomer);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var uniqueId2 = Guid.NewGuid().ToString("N")[..8];
        var duplicatePayload = new
        {
            name = $"Second User {uniqueId2}",
            personalNum = sharedPersonalNum, // Same personal number
            email = $"second_{uniqueId2}@example.com",
            phoneNumber = "+46702223344"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", duplicatePayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        jsonDoc.RootElement.GetProperty("status").GetInt32().Should().Be(409);
        jsonDoc.RootElement.GetProperty("code").GetString().Should().Be("PERSONAL_NUM_TAKEN");
    }

    [Fact]
    public async Task Register_InvalidPersonalNum_LuhnCheckFailed_Returns_400ValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var payload = new
        {
            name = $"Invalid User {uniqueId}",
            personalNum = "199001019999", // Invalid Luhn checksum
            email = $"invalid_{uniqueId}@example.com",
            phoneNumber = "+46701112233"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        jsonDoc.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        jsonDoc.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.TryGetProperty("PersonalNum", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Register_UnderageCustomer_Returns_400ValidationProblemDetails()
    {
        // Arrange: Born 5 years ago (under 18)
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var underageBirthDate = DateTime.UtcNow.AddYears(-5);
        var digits = $"{underageBirthDate:yyMMdd}123";
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var n = (digits[i] - '0') * (i % 2 == 0 ? 2 : 1);
            sum += n > 9 ? n - 9 : n;
        }
        var checkDigit = (10 - sum % 10) % 10;
        var underagePersonalNum = $"{underageBirthDate:yyyy}{digits[2..]}{checkDigit}";

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var payload = new
        {
            name = $"Underage User {uniqueId}",
            personalNum = underagePersonalNum,
            email = $"underage_{uniqueId}@example.com",
            phoneNumber = "+46701112233"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        jsonDoc.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        jsonDoc.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.TryGetProperty("PersonalNum", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Logout_Clears_AuthCookie()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        // Act
        var response = await client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").Should().BeTrue();

        var setCookieHeader = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        setCookieHeader.Should().NotBeNull();
        setCookieHeader.Should().Contain("access_token=");
    }

    [Fact]
    public async Task Register_Then_BankIdLogin_LogsIn_Registered_Customer()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var customPersonalNum = CreateRandomPersonalNum();
        var customEmail = $"customer_{uniqueId}@example.com";

        var registerPayload = new
        {
            name = $"New Customer {uniqueId}",
            personalNum = customPersonalNum,
            email = customEmail,
            phoneNumber = "+46709998877"
        };

        // Act 1: Register customer
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerPayload);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: Clear cookie via logout to simulate logging in fresh
        await client.PostAsync("/api/auth/logout", null);

        // Act 3: Initiate and Collect BankID with the newly registered customer's personal number
        var initiateResponse = await client.PostAsJsonAsync("/api/auth/bankid/initiate", new
        {
            personalNum = customPersonalNum
        });
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initiateContent = await initiateResponse.Content.ReadAsStringAsync();
        using var initDoc = JsonDocument.Parse(initiateContent);
        var orderRef = initDoc.RootElement.GetProperty("orderRef").GetString();

        HttpResponseMessage collectResponse = null!;
        for (var i = 0; i < 15; i++)
        {
            collectResponse = await client.PostAsJsonAsync("/api/auth/bankid/collect", new
            {
                orderRef = orderRef
            });

            var content = await collectResponse.Content.ReadAsStringAsync();
            if (!collectResponse.IsSuccessStatusCode)
            {
                break;
            }

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("status", out var s) || doc.RootElement.TryGetProperty("Status", out s))
            {
                if (string.Equals(s.GetString(), "COMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            await Task.Delay(200);
        }
        collectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 4: Query /api/auth/me to confirm it is the newly registered customer
        var meResponse = await client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meContent = await meResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(meContent);
        var email = jsonDoc.RootElement.GetProperty("email").GetString();
        email.Should().Be(customEmail);
    }

    [Fact]
    public async Task EmailPassword_Login_Sets_HttpOnly_AuthCookie_And_Returns_Customer()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        // Act 1: Login with seeded customer Anna
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "anna@exempel.se",
            password = "password123"
        });

        // Assert 1: Successful login and auth cookie set
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        loginResponse.Headers.Contains("Set-Cookie").Should().BeTrue();

        var content = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var name = doc.RootElement.GetProperty("name").GetString();
        name.Should().Be("Anna Smith");

        // Act 2: Access protected /api/auth/me endpoint
        var meResponse = await client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meContent = await meResponse.Content.ReadAsStringAsync();
        using var meDoc = JsonDocument.Parse(meContent);
        var email = meDoc.RootElement.GetProperty("email").GetString();
        email.Should().Be("anna@exempel.se");
    }

    [Fact]
    public async Task RefreshSession_WhenAuthenticated_Refreshes_AuthCookie()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        // Act 1: Login with seeded customer Anna
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "anna@exempel.se",
            password = "password123"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: Call /api/auth/refresh to extend session
        var refreshResponse = await client.PostAsync("/api/auth/refresh", null);

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshResponse.Headers.Contains("Set-Cookie").Should().BeTrue();

        var setCookieHeader = refreshResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
        setCookieHeader.Should().NotBeNull();
        setCookieHeader.Should().Contain("access_token=");
        setCookieHeader.Should().Contain("httponly");
        setCookieHeader.Should().Contain("path=/");

        var refreshContent = await refreshResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(refreshContent);
        var name = doc.RootElement.GetProperty("name").GetString();
        name.Should().Be("Anna Smith");
    }

    [Fact]
    public async Task RefreshSession_WhenUnauthenticated_Returns_Unauthorized()
    {
        // Arrange: Client without cookies
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

        // Act
        var refreshResponse = await client.PostAsync("/api/auth/refresh", null);

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string CreateRandomPersonalNum()
    {
        var birthDate = new DateTime(1970, 1, 1).AddDays(Random.Shared.Next(0, 365 * 30));
        var digits = $"{birthDate:yyMMdd}{Random.Shared.Next(0, 1000):D3}";

        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var n = (digits[i] - '0') * (i % 2 == 0 ? 2 : 1);
            sum += n > 9 ? n - 9 : n;
        }
        var checkDigit = (10 - sum % 10) % 10;

        return $"{birthDate:yyyy}{digits[2..]}{checkDigit}";
    }
}
