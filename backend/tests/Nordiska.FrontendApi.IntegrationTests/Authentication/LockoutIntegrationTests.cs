using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.FrontendApi.IntegrationTests.Authentication;

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
    public Task DisposeAsync() => PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);

    private async Task<Customer> CreateCustomerAsync(string? password)
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "lockout", password);
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
}
