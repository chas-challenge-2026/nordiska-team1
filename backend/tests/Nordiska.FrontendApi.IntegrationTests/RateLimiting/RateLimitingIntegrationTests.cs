using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Nordiska.FrontendApi.IntegrationTests.Authentication;

namespace Nordiska.FrontendApi.IntegrationTests.RateLimiting;

// Low auth limit so we can hit it quickly. Transactions limit is kept high so it does not interfere.
public sealed class LowAuthLimitWebApplicationFactory : CustomAuthWebApplicationFactory
{
    public const int AuthPermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:Auth:PermitLimit", AuthPermitLimit.ToString());
    }
}

// Low transactions limit. Auth limit stays high since the tests need to log in via BankID first.
public sealed class LowTransactionsLimitWebApplicationFactory : CustomAuthWebApplicationFactory
{
    public const int TransactionsPermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:Transactions:PermitLimit", TransactionsPermitLimit.ToString());
    }
}

public class AuthRateLimitingIntegrationTests : IClassFixture<LowAuthLimitWebApplicationFactory>
{
    private readonly LowAuthLimitWebApplicationFactory _factory;

    public AuthRateLimitingIntegrationTests(LowAuthLimitWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ExceedingLimit_Returns429WithRetryAfterAndProblemDetails()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var payload = new { email = "unknown@example.com", password = "wrong" };

        // Use up all permits for this IP (failed logins count too)
        for (var i = 0; i < LowAuthLimitWebApplicationFactory.AuthPermitLimit; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", payload);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var res = await client.PostAsJsonAsync("/api/auth/login", payload);

        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        Assert.NotNull(res.Headers.RetryAfter);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(429, doc.RootElement.GetProperty("status").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task BankIdCollect_IsNotRateLimited()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        // Collect is polled during a normal BankID flow, so it must never hit the auth limit
        for (var i = 0; i < LowAuthLimitWebApplicationFactory.AuthPermitLimit + 2; i++)
        {
            var res = await client.PostAsJsonAsync("/api/auth/bankid/collect", new { orderRef = Guid.NewGuid().ToString() });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, res.StatusCode);
        }
    }
}

public class TransactionsRateLimitingIntegrationTests : IClassFixture<LowTransactionsLimitWebApplicationFactory>
{
    private const string AnnaPersonalNum = "198202116050";
    private const string ErikPersonalNum = "197903142380";
    // Account 3 belongs to Erik, so Anna gets 403 and no ledger data is changed in the shared test stores
    private const long ErikAccountId = 3;

    private readonly LowTransactionsLimitWebApplicationFactory _factory;

    public TransactionsRateLimitingIntegrationTests(LowTransactionsLimitWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateTransaction_ExceedingLimit_IsPerCustomer()
    {
        var anna = await LoginWithBankIdAsync(AnnaPersonalNum);
        var erik = await LoginWithBankIdAsync(ErikPersonalNum);
        var req = new { accountId = ErikAccountId, type = "Withdrawal", amount = 1m };

        for (var i = 0; i < LowTransactionsLimitWebApplicationFactory.TransactionsPermitLimit; i++)
        {
            var allowed = await anna.PostAsJsonAsync("/api/transactions", req);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var annaRes = await anna.PostAsJsonAsync("/api/transactions", req);
        var erikRes = await erik.PostAsJsonAsync("/api/transactions/transfer", new { sourceAccountId = 1L, targetAccountId = 2L, amount = 1m });

        Assert.Equal(HttpStatusCode.TooManyRequests, annaRes.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, erikRes.StatusCode);
    }

    [Fact]
    public async Task MoneyMovingEndpoints_ShareTheSameLimit()
    {
        var erik = await LoginWithBankIdAsync(ErikPersonalNum);
        // Account 1 belongs to Anna, so all of these return 403 for Erik without touching data
        var endpoints = new (string Url, object Body)[]
        {
            ("/api/transactions", new { accountId = 1L, type = "Deposit", amount = 1m }),
            ("/api/transactions/transfer", new { sourceAccountId = 1L, targetAccountId = 2L, amount = 1m }),
            ("/api/transactions/planned", new { accountId = 1L, type = "Deposit", amount = 1m, scheduledAt = DateTime.UtcNow.AddDays(1) })
        };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i <= LowTransactionsLimitWebApplicationFactory.TransactionsPermitLimit; i++)
        {
            var (url, body) = endpoints[i % endpoints.Length];
            statuses.Add((await erik.PostAsJsonAsync(url, body)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses.Last());
    }

    [Fact]
    public async Task GetTransactions_IsNotRateLimited()
    {
        var anna = await LoginWithBankIdAsync(AnnaPersonalNum);

        for (var i = 0; i < LowTransactionsLimitWebApplicationFactory.TransactionsPermitLimit + 2; i++)
        {
            var res = await anna.GetAsync("/api/transactions");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }

    private async Task<HttpClient> LoginWithBankIdAsync(string personalNum)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

        var initiate = await client.PostAsJsonAsync("/api/auth/bankid/initiate", new { personalNum });
        initiate.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await initiate.Content.ReadAsStringAsync());
        var orderRef = doc.RootElement.GetProperty("orderRef").GetString();

        // The test auth service completes on the first collect
        var collect = await client.PostAsJsonAsync("/api/auth/bankid/collect", new { orderRef });
        collect.EnsureSuccessStatusCode();

        return client;
    }
}
