using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.FrontendApi.IntegrationTests.Authentication;

// These tests need a real Postgres (the real AuthService + UserManager are used).
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

// Real services, no fakes. Auth limit is raised so the rate limiter never answers before the lockout does.
public sealed class PostgresAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Auth:PermitLimit"] = "10000",
                ["ActiveLogin:BankId:Environment"] = "Simulated"
            });
        });
    }
}

// Every test class starts its own host and runs the EF migrations on startup. In parallel they race against
// the same CI database, so these tests run alone (after the parallel ones) to get a fully migrated database.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresAuthWebApplicationFactory>
{
    public const string Name = "Postgres";
}

[Collection(PostgresCollection.Name)]
public class LockoutIntegrationTests : IAsyncLifetime
{
    private const string CorrectPassword = "Korrekt-Losenord-1";
    private const int MaxFailedAttempts = 5;

    private readonly PostgresAuthWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly List<long> _createdCustomerIds = new();

    public LockoutIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    [PostgresFact]
    public async Task Login_FiveWrongPasswords_Returns423WithRetryAfterAndProblemDetails()
    {
        var customer = await CreateCustomerAsync(CorrectPassword);

        HttpResponseMessage res = null!;
        for (var i = 0; i < MaxFailedAttempts; i++)
        {
            res = await LoginAsync(customer.Email!, "fel-losenord");
        }

        Assert.Equal((HttpStatusCode)StatusCodes.Status423Locked, res.StatusCode);
        Assert.NotNull(res.Headers.RetryAfter);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(423, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("tillfälligt spärrat", doc.RootElement.GetProperty("detail").GetString());
        Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
    }

    [PostgresFact]
    public async Task Login_CorrectPasswordWhileLockedOut_Returns423()
    {
        var customer = await CreateCustomerAsync(CorrectPassword);
        await FailLoginAsync(customer.Email!, MaxFailedAttempts);

        var res = await LoginAsync(customer.Email!, CorrectPassword);

        Assert.Equal((HttpStatusCode)StatusCodes.Status423Locked, res.StatusCode);
    }

    [PostgresFact]
    public async Task Login_SuccessAfterFourFailures_ResetsFailedCount()
    {
        var customer = await CreateCustomerAsync(CorrectPassword);
        await FailLoginAsync(customer.Email!, MaxFailedAttempts - 1);

        var ok = await LoginAsync(customer.Email!, CorrectPassword);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // If the count was not reset, the first of these would lock the account
        for (var i = 0; i < MaxFailedAttempts - 1; i++)
        {
            var res = await LoginAsync(customer.Email!, "fel-losenord");
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }
    }

    [PostgresFact]
    public async Task Login_DevPassword_DoesNotWorkForCustomerWithRealPassword()
    {
        var customer = await CreateCustomerAsync(CorrectPassword);

        var res = await LoginAsync(customer.Email!, "password123");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [PostgresFact]
    public async Task Login_DevPassword_WorksForCustomerWithoutPasswordInDevelopment()
    {
        // CI runs the tests with ASPNETCORE_ENVIRONMENT=Development
        var customer = await CreateCustomerAsync(password: null);

        var res = await LoginAsync(customer.Email!, "password123");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [PostgresFact]
    public async Task BankIdLogin_ResetsLockout()
    {
        var customer = await CreateCustomerAsync(CorrectPassword);
        await FailLoginAsync(customer.Email!, MaxFailedAttempts);

        await LoginWithBankIdAsync(customer.PersonalNum);
        var res = await LoginAsync(customer.Email!, CorrectPassword);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // Remove the customers this test created so the shared CI database stays clean
    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();

        foreach (var id in _createdCustomerIds)
        {
            var customer = await userManager.FindByIdAsync(id.ToString());
            if (customer is not null) await userManager.DeleteAsync(customer);
        }
    }

    private async Task<Customer> CreateCustomerAsync(string? password)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var customer = new Customer
        {
            UserName = $"lockout_{uniqueId}@example.com",
            Email = $"lockout_{uniqueId}@example.com",
            Name = $"Lockout Test {uniqueId}",
            PersonalNum = CreateRandomPersonalNum(),
            CreatedAt = DateTime.UtcNow
        };

        var result = password is null
            ? await userManager.CreateAsync(customer)
            : await userManager.CreateAsync(customer, password);

        Assert.True(result.Succeeded, string.Join(';', result.Errors.Select(e => e.Description)));
        _createdCustomerIds.Add(customer.Id);
        return customer;
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password)
        => _client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task FailLoginAsync(string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await LoginAsync(email, "fel-losenord");
        }
    }

    private async Task LoginWithBankIdAsync(string personalNum)
    {
        var initiate = await _client.PostAsJsonAsync("/api/auth/bankid/initiate", new { personalNum });
        Assert.Equal(HttpStatusCode.OK, initiate.StatusCode);

        using var initDoc = JsonDocument.Parse(await initiate.Content.ReadAsStringAsync());
        var orderRef = initDoc.RootElement.GetProperty("orderRef").GetString();

        // The simulated BankID client goes through a few pending states before COMPLETE
        for (var i = 0; i < 40; i++)
        {
            var collect = await _client.PostAsJsonAsync("/api/auth/bankid/collect", new { orderRef });
            Assert.Equal(HttpStatusCode.OK, collect.StatusCode);

            using var doc = JsonDocument.Parse(await collect.Content.ReadAsStringAsync());
            if (string.Equals(doc.RootElement.GetProperty("status").GetString(), "COMPLETE", StringComparison.OrdinalIgnoreCase))
                return;

            await Task.Delay(250);
        }

        Assert.Fail("BankID login never reached COMPLETE.");
    }

    // Valid Swedish personal number (YYYYMMDDNNNC) with a correct Luhn check digit, so BankID accepts it
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
