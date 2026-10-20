using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.FrontendApi.Endpoints.Reporting;
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.FrontendApi.IntegrationTests.Reporting;

// Real Postgres (real ReportingDbContext, real HMAC signing). CI sets RUN_POSTGRES_TESTS=true,
// locally these are skipped unless you set it yourself. Shares PostgresCollection with the
// other Postgres tests so they run against the same migrated database, never in parallel.
[Collection(PostgresCollection.Name)]
public class AuditIntegrationTests : IAsyncLifetime
{
    private readonly PostgresAuthWebApplicationFactory _factory;
    private readonly List<long> _createdAuditEntryIds = new();
    private readonly List<long> _createdCustomerIds = new();

    public AuditIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task GetAuditEntries_ForOwnUser_ReturnsOnlyOwnEntries()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var other = await PostgresTestData.CreateCustomerAsync(_factory.Services, "audit");
        _createdCustomerIds.Add(other.Id);

        var ownEntry = await SeedAuditEntryAsync("CUSTOMER_UPDATE", customer.Id);
        await SeedAuditEntryAsync("CUSTOMER_UPDATE", other.Id);

        var response = await client.GetAsync("/api/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuditEntriesResponse>();
        body.Should().NotBeNull();
        body!.Items.Select(i => i.Id).Should().Contain(ownEntry.Id);
        body.Items.Should().OnlyContain(i => i.UserId == customer.Id);
    }

    [PostgresFact]
    public async Task GetAuditEntries_AsAdmin_CanFilterByUserId()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "audit");
        _createdCustomerIds.Add(customer.Id);
        var entry = await SeedAuditEntryAsync("CUSTOMER_UPDATE", customer.Id);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        var response = await client.GetAsync($"/api/audit?userId={customer.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuditEntriesResponse>();
        body!.Items.Select(i => i.Id).Should().Contain(entry.Id);
        body.Items.Should().OnlyContain(i => i.UserId == customer.Id);
    }

    [PostgresFact]
    public async Task VerifyAuditEntry_ValidEntry_ReturnsIsValidTrue()
    {
        var (client, customer) = await CreateLoggedInCustomerAsync();
        var entry = await SeedAuditEntryAsync("CUSTOMER_UPDATE", customer.Id);

        var response = await client.GetAsync($"/api/audit/{entry.Id}/verify");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuditVerificationResponse>();
        body!.IsValid.Should().BeTrue();
        body.Signature.Should().Be(entry.Signature);
    }

    [PostgresFact]
    public async Task VerifyAuditEntry_UnknownId_Returns404NotFound()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();

        var response = await client.GetAsync("/api/audit/999999999/verify");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [PostgresFact]
    public async Task VerifyAuditEntry_OtherUsersEntry_AsNonAdmin_Returns403Forbidden()
    {
        var (client, _) = await CreateLoggedInCustomerAsync();
        var other = await PostgresTestData.CreateCustomerAsync(_factory.Services, "audit");
        _createdCustomerIds.Add(other.Id);
        var entry = await SeedAuditEntryAsync("CUSTOMER_UPDATE", other.Id);

        var response = await client.GetAsync($"/api/audit/{entry.Id}/verify");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [PostgresFact]
    public async Task VerifyAuditEntry_OtherUsersEntry_AsAdmin_Returns200Ok()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "audit");
        _createdCustomerIds.Add(customer.Id);
        var entry = await SeedAuditEntryAsync("CUSTOMER_UPDATE", customer.Id);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAdminToken());

        var response = await client.GetAsync($"/api/audit/{entry.Id}/verify");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // Remove everything this test class created so the shared CI database stays clean
    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var entries = await db.AuditEntries.Where(e => _createdAuditEntryIds.Contains(e.Id)).ToListAsync();
        db.AuditEntries.RemoveRange(entries);
        await db.SaveChangesAsync();

        await PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);
    }

    private async Task<(HttpClient Client, Customer Customer)> CreateLoggedInCustomerAsync()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "audit");
        _createdCustomerIds.Add(customer.Id);

        var client = await PostgresTestData.CreateLoggedInClientAsync(_factory, customer);
        return (client, customer);
    }

    private async Task<AuditEntry> SeedAuditEntryAsync(string action, long userId)
    {
        using var scope = _factory.Services.CreateScope();
        var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

        var entry = await auditLogService.LogAsync(action, userId, "{}");
        _createdAuditEntryIds.Add(entry.Id);
        return entry;
    }

    // JwtProvider never issues the Admin role through real login, so it can only be reached
    // by minting a token the same way JwtProvider does, with that role added.
    private string CreateAdminToken()
    {
        using var scope = _factory.Services.CreateScope();
        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Random.Shared.Next(1000, 999999).ToString()),
            new(JwtRegisteredClaimNames.Email, "admin@exempel.se"),
            new(ClaimTypes.Role, "Admin")
        };

        var key = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(jwtOptions.TokenLifetimeInMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
