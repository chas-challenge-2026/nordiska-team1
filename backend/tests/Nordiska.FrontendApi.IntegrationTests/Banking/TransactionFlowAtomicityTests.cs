using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Npgsql;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

public partial class TransactionFlowIntegrationTests
{
    [PostgresFact]
    public async Task ConcurrentHttpWithdrawals_OneSucceedsAndOneReturnsConflict()
    {
        var (_, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await SeedAccountAsync(customer.Id, 100m);
        await using var app = AtomicityApp(new HttpMoneyGate());
        using var client = await PostgresTestData.CreateLoggedInClientAsync(app, customer);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/transactions", new TransactionRequest(accountId, "withdrawal", 80m)),
            client.PostAsJsonAsync("/api/transactions", new TransactionRequest(accountId, "withdrawal", 80m)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(20m, (await GetLedgerEntriesAsync(accountId)).Sum(entry => entry.Amount));
        Assert.Equal(20m, (await GetStoredAccountAsync(accountId)).Balance);
    }

    [PostgresFact]
    public async Task ConcurrentHttpTransfers_OneTransferIsCommitted()
    {
        var (_, customer) = await CreateLoggedInCustomerAsync();
        var source = await SeedAccountAsync(customer.Id, 100m);
        var target = await SeedAccountAsync(customer.Id);
        await using var app = AtomicityApp(new HttpMoneyGate());
        using var client = await PostgresTestData.CreateLoggedInClientAsync(app, customer);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/transactions/transfer", new TransferRequest(source, target, 80m)),
            client.PostAsJsonAsync("/api/transactions/transfer", new TransferRequest(source, target, 80m)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(20m, (await GetStoredAccountAsync(source)).Balance);
        Assert.Equal(80m, (await GetStoredAccountAsync(target)).Balance);
        Assert.Equal(20m, (await GetLedgerEntriesAsync(source)).Sum(entry => entry.Amount));
        Assert.Equal(80m, (await GetLedgerEntriesAsync(target)).Sum(entry => entry.Amount));
    }

    [PostgresFact]
    public async Task HttpTransfer_WriteFailure_LeavesBothAccountsUnchanged()
    {
        var (_, customer) = await CreateLoggedInCustomerAsync();
        var source = await SeedAccountAsync(customer.Id, 100m);
        var target = await SeedAccountAsync(customer.Id);
        await using var app = AtomicityApp(failAfterWrite: true);
        using var client = await PostgresTestData.CreateLoggedInClientAsync(app, customer);

        var response = await client.PostAsJsonAsync("/api/transactions/transfer", new TransferRequest(source, target, 80m));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Single(await GetLedgerEntriesAsync(source));
        Assert.Empty(await GetLedgerEntriesAsync(target));
        Assert.Equal(100m, (await GetStoredAccountAsync(source)).Balance);
        Assert.Equal(0m, (await GetStoredAccountAsync(target)).Balance);
    }

    [PostgresFact]
    public async Task HttpWithdrawal_RepeatedSerializationFailures_ReturnsConflictWithoutPosting()
    {
        var (_, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await SeedAccountAsync(customer.Id, 100m);
        await using var app = AtomicityApp(failSerialization: true);
        using var client = await PostgresTestData.CreateLoggedInClientAsync(app, customer);

        var response = await client.PostAsJsonAsync("/api/transactions", new TransactionRequest(accountId, "withdrawal", 80m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await GetLedgerEntriesAsync(accountId));
        Assert.Equal(100m, (await GetStoredAccountAsync(accountId)).Balance);
    }

    [PostgresFact]
    public async Task ServiceProcessingFuturePlan_DoesNotExecuteOrConsumeIt()
    {
        var (_, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await SeedAccountAsync(customer.Id, 100m);
        long planId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
            var plan = new LedgerEntry
            {
                AccountId = accountId, Type = "withdrawal", Amount = 10m,
                IsPlanned = true, PlannedDate = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow
            };
            db.LedgerEntries.Add(plan);
            await db.SaveChangesAsync();
            planId = plan.Id;
        }
        using var processing = _factory.Services.CreateScope();
        var service = processing.ServiceProvider.GetRequiredService<Nordiska.Modules.Banking.Application.ITransactionService>();

        Assert.Null(await service.ProcessPlannedTransactionAsync(planId));

        Assert.Equal(100m, (await GetStoredAccountAsync(accountId)).Balance);
        Assert.Contains(await GetLedgerEntriesAsync(accountId), entry => entry.Id == planId && entry.IsPlanned);
    }

    [PostgresFact]
    public async Task HttpCancellation_ExecutedEntry_ReturnsConflictAndKeepsLedger()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await SeedAccountAsync(customer.Id, 100m);
        var entry = Assert.Single(await GetLedgerEntriesAsync(accountId));

        var response = await client.DeleteAsync($"/api/transactions/planned/{entry.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await GetLedgerEntriesAsync(accountId));
        Assert.Equal(100m, (await GetStoredAccountAsync(accountId)).Balance);
    }

    [PostgresFact]
    public async Task ConcurrentServiceWorkers_ExecuteRecurringOccurrenceOnce()
    {
        var (_, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await SeedAccountAsync(customer.Id, 100m);
        var planId = await SeedDuePlanAsync(accountId, recurring: true);
        var gate = new HttpMoneyGate();
        await using var first = WorkerContext(new WorkerPlanInterceptor(gate));
        await using var second = WorkerContext(new WorkerPlanInterceptor(gate));
        var firstService = WorkerService(first);
        var secondService = WorkerService(second);

        var results = await Task.WhenAll(firstService.ProcessPlannedTransactionAsync(planId), secondService.ProcessPlannedTransactionAsync(planId));

        Assert.Single(results, result => result is not null);
        Assert.Equal(90m, (await GetStoredAccountAsync(accountId)).Balance);
        var entries = await GetLedgerEntriesAsync(accountId);
        Assert.Equal(90m, entries.Where(entry => !entry.IsPlanned).Sum(entry => entry.Amount));
        Assert.Equal(DateTime.UtcNow.Date.AddDays(-1).AddMonths(1), entries.Single(entry => entry.Id == planId).PlannedDate);
    }

    [PostgresFact]
    public async Task ServiceCancellationRacingWithWorker_PreservesFundsAndPlanConsistency()
    {
        var (_, customer) = await CreateLoggedInCustomerAsync();
        var accountId = await SeedAccountAsync(customer.Id, 100m);
        var planId = await SeedDuePlanAsync(accountId);
        var gate = new HttpMoneyGate();
        await using var processing = WorkerContext(new WorkerPlanInterceptor(gate));
        await using var cancelling = WorkerContext(new WorkerPlanInterceptor(gate));
        var execution = WorkerService(processing).ProcessPlannedTransactionAsync(planId);
        var cancellation = WorkerService(cancelling).CancelPlannedAsync(planId);

        await Task.WhenAll(execution, cancellation);

        var executed = execution.Result is not null;
        Assert.Equal(!executed, cancellation.Result);
        var entries = await GetLedgerEntriesAsync(accountId);
        Assert.DoesNotContain(entries, entry => entry.IsPlanned);
        Assert.Equal(executed ? 90m : 100m, entries.Sum(entry => entry.Amount));
        Assert.Equal(executed ? 90m : 100m, (await GetStoredAccountAsync(accountId)).Balance);
    }

    private async Task<long> SeedDuePlanAsync(long accountId, bool recurring = false)
    {
        await using var db = WorkerContext();
        var plan = new LedgerEntry
        {
            AccountId = accountId, Type = "withdrawal", Amount = 10m,
            IsPlanned = true, PlannedDate = DateTime.UtcNow.Date.AddDays(-1),
            Repeating = recurring ? "month" : null, CreatedAt = DateTime.UtcNow
        };
        db.LedgerEntries.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    private BankingDbContext WorkerContext(params IInterceptor[] interceptors)
    {
        using var scope = _factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<BankingDbContext>().Database.GetDbConnection().ConnectionString;
        return new BankingDbContext(new DbContextOptionsBuilder<BankingDbContext>().UseNpgsql(connection)
            .AddInterceptors(interceptors).Options);
    }

    private static TransactionService WorkerService(BankingDbContext db)
        => new(new TransactionRepository(db), new SavingsAccountRepository(db), NullLogger<TransactionService>.Instance);

    private sealed class WorkerPlanInterceptor(HttpMoneyGate gate) : DbCommandInterceptor
    {
        private bool _gated;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!_gated && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
                && command.CommandText.Contains("ledger_entries", StringComparison.Ordinal))
            {
                _gated = true;
                await gate.ArriveAsync(cancellationToken);
            }
            return result;
        }
    }

    private WebApplicationFactory<Program> AtomicityApp(
        HttpMoneyGate? gate = null, bool failAfterWrite = false, bool failSerialization = false)
    {
        using var scope = _factory.Services.CreateScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<BankingDbContext>()
            .Database.GetDbConnection().ConnectionString;
        return _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddHttpContextAccessor();
            services.RemoveAll<DbContextOptions<BankingDbContext>>();
            services.AddDbContext<BankingDbContext>((provider, options) => options.UseNpgsql(connectionString).AddInterceptors(
                new HttpMoneyInterceptor(provider.GetRequiredService<IHttpContextAccessor>(), gate, failAfterWrite, failSerialization)));
        }));
    }

    private sealed class HttpMoneyGate
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    private sealed class HttpMoneyInterceptor(
        IHttpContextAccessor accessor, HttpMoneyGate? gate, bool failAfterWrite, bool failSerialization) : DbCommandInterceptor
    {
        private int _ledgerReads;
        private bool _gated;
        private bool IsMoneyRequest => accessor.HttpContext?.Request is { Method: "POST" } request
            && (request.Path == "/api/transactions" || request.Path == "/api/transactions/transfer");

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (IsMoneyRequest && command.CommandText.Contains("ledger_entries", StringComparison.Ordinal))
            {
                if (failAfterWrite && command.CommandText.Contains("INSERT", StringComparison.Ordinal))
                    throw new InvalidOperationException("Injected failure after database write");
                if (gate is not null && !_gated && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
                    && ++_ledgerReads == 2)
                {
                    _gated = true;
                    await gate.ArriveAsync(cancellationToken);
                }
            }
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (IsMoneyRequest && failSerialization && command.CommandText.Contains("INSERT", StringComparison.Ordinal)
                && command.CommandText.Contains("ledger_entries", StringComparison.Ordinal))
                throw new PostgresException("Injected contention", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
