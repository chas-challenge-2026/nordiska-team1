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
            Id = 1,
            Name = "Anna Smith",
            Email = "anna@exempel.se",
            PersonalNum = "198202116050",
            PhoneNumber = "+46701112233"
        };
        var erik = new Customer
        {
            Id = 2,
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
        var cleanPersonalNum = request.PersonalNum.Replace("-", "").Trim();
        var customer = new Customer
        {
            Id = Random.Shared.Next(100, 9999),
            Name = request.Name,
            Email = request.Email,
            PersonalNum = cleanPersonalNum,
            PhoneNumber = request.PhoneNumber
        };

        _customers[cleanPersonalNum] = customer;
        _customers[request.Email] = customer;

        var token = await _jwtProvider.Generate(customer);
        response.AppendAuthCookie(token, 15);

        return new AuthenticationResultDto(true, null, Token: token);
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
}

public class TestSavingsAccountRepository : ISavingsAccountRepository
{
    private static readonly List<SavingsAccount> _store = new()
    {
        new SavingsAccount { Id = 1, CustomerId = 1, AccountNumber = "NOR-100001", AccountType = "saving", AccountName = "Sparkonto", Balance = 5000m, InterestRate = 0.025m, CreatedAt = DateTime.UtcNow },
        new SavingsAccount { Id = 2, CustomerId = 1, AccountNumber = "NOR-100002", AccountType = "checking", AccountName = "Lönekonto", Balance = 10000m, InterestRate = 0.005m, CreatedAt = DateTime.UtcNow },
        new SavingsAccount { Id = 3, CustomerId = 2, AccountNumber = "NOR-200001", AccountType = "saving", AccountName = "Eriks Spar", Balance = 3000m, InterestRate = 0.025m, CreatedAt = DateTime.UtcNow }
    };
    private static long _next = 10;

    public Task<IEnumerable<SavingsAccount>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<SavingsAccount>>(_store.ToList());

    public Task<IEnumerable<SavingsAccount>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<SavingsAccount>>(_store.Where(s => s.CustomerId == customerId).ToList());

    public Task<SavingsAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.FirstOrDefault(s => s.Id == id));

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

    public Task<long> CreateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        entry.Id = _next++;
        _store.Add(entry);
        return Task.FromResult(entry.Id);
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
}

public class TestCustomerService : ICustomerService
{
    private static readonly ConcurrentDictionary<long, Customer> _customers = new();

    static TestCustomerService()
    {
        var anna = new Customer { Id = 1, Name = "Anna Smith", Email = "anna@exempel.se", PersonalNum = "198202116050", PhoneNumber = "+46701112233", CreatedAt = DateTime.UtcNow };
        var erik = new Customer { Id = 2, Name = "Erik Svensson", Email = "erik@exempel.se", PersonalNum = "197903142380", PhoneNumber = "+46702223344", CreatedAt = DateTime.UtcNow };
        _customers[1] = anna;
        _customers[2] = erik;
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

    public Task<Customer> UpdateAsync(long id, string? name, string? email, string? personalNum, string? phoneNumber = null, CancellationToken cancellationToken = default)
    {
        var customer = _customers.GetOrAdd(id, k => new Customer { Id = k, Name = "User", Email = "u@ex.se", PersonalNum = "198001010000" });
        if (!string.IsNullOrWhiteSpace(name)) customer.Name = name;
        if (!string.IsNullOrWhiteSpace(email)) customer.Email = email;
        if (!string.IsNullOrWhiteSpace(personalNum)) customer.PersonalNum = personalNum;
        if (phoneNumber != null) customer.PhoneNumber = phoneNumber;
        customer.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(customer);
    }

    public Task<Customer> PatchProfileAsync(long id, string? name, string? email, string? phoneNumber = null, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(id, name, email, null, phoneNumber, cancellationToken);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        _customers.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public class CustomAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Tests share one factory per class, so raise the limits to keep rate limiting out of the way (NOR-70)
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
        var registerPayload = new
        {
            name = $"Test Person {uniqueId}",
            personalNum = $"19900101{Random.Shared.Next(1000, 9999)}",
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
        var customPersonalNum = $"19910203{Random.Shared.Next(1000, 9999)}";
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
}
