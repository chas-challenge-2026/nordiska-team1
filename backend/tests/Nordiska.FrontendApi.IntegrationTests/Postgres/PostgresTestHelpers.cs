using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.FrontendApi.IntegrationTests.Postgres;

// These tests need a real Postgres (real services, real DbContexts, real UserManager).
// CI sets RUN_POSTGRES_TESTS=true, locally they are skipped unless you set it yourself.
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "RUN_POSTGRES_TESTS";

    public PostgresFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"Kräver Postgres. Sätt {EnvironmentVariable}=true för att köra.";
        }
    }
}

// Real services, no fakes. Limits are raised so the rate limiter never answers before the lockout does,
// and so the transaction tests don't hit the 10/min per customer limit.
public sealed class PostgresAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "10000");
        builder.UseSetting("RateLimiting:Transactions:PermitLimit", "10000");
    }
}

// Every test class starts its own host and runs the EF migrations on startup. In parallel they race against
// the same CI database, so these tests run alone (after the parallel ones) to get a fully migrated database.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresAuthWebApplicationFactory>
{
    public const string Name = "Postgres";
}

public static class PostgresTestData
{
    public const string Password = "Korrekt-Losenord-1";

    public static async Task<Customer> CreateCustomerAsync(IServiceProvider services, string prefix, string? password = Password)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var customer = new Customer
        {
            UserName = $"{prefix}_{uniqueId}@example.com",
            Email = $"{prefix}_{uniqueId}@example.com",
            Name = $"{prefix} Test {uniqueId}",
            PersonalNum = CreateRandomPersonalNum(),
            CreatedAt = DateTime.UtcNow
        };

        var result = password is null
            ? await userManager.CreateAsync(customer)
            : await userManager.CreateAsync(customer, password);

        Assert.True(result.Succeeded, string.Join(';', result.Errors.Select(e => e.Description)));
        return customer;
    }

    public static async Task<HttpClient> CreateLoggedInClientAsync(WebApplicationFactory<Program> factory, Customer customer, string password = Password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(customer.Email!, password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return client;
    }

    // Everything in banking is DeleteBehavior.Restrict, so ledger entries go first, then accounts, then the customer
    public static async Task DeleteCustomersAsync(IServiceProvider services, IEnumerable<long> customerIds)
    {
        var ids = customerIds.ToList();
        if (ids.Count == 0) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();

        var accountIds = await db.SavingsAccounts
            .Where(a => ids.Contains(a.CustomerId))
            .Select(a => a.Id)
            .ToListAsync();

        await db.LedgerEntries.Where(e => accountIds.Contains(e.AccountId)).ExecuteDeleteAsync();
        await db.SavingsAccounts.Where(a => accountIds.Contains(a.Id)).ExecuteDeleteAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();
        foreach (var id in ids)
        {
            var customer = await userManager.FindByIdAsync(id.ToString());
            if (customer is not null) await userManager.DeleteAsync(customer);
        }
    }

    // Valid Swedish personal number (YYYYMMDDNNNC) with a correct Luhn check digit, so BankID accepts it
    public static string CreateRandomPersonalNum()
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
