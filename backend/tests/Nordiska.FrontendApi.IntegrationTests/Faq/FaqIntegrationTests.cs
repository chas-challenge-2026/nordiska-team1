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
using Nordiska.FrontendApi.IntegrationTests.Postgres;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Contracts.Responses;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.Db;

namespace Nordiska.FrontendApi.IntegrationTests.Faq;

// Real Postgres (real FaqDbContext, real UserManager). CI sets RUN_POSTGRES_TESTS=true,
// locally these are skipped unless you set it yourself. Shares PostgresCollection with the
// other Postgres tests so they run against the same migrated database, never in parallel.
[Collection(PostgresCollection.Name)]
public class FaqIntegrationTests : IAsyncLifetime
{
    private readonly PostgresAuthWebApplicationFactory _factory;
    private readonly List<int> _createdFaqIds = new();
    private readonly List<long> _createdCustomerIds = new();

    public FaqIntegrationTests(PostgresAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task GetById_ExistingEntry_ReturnsEntryFromDatabase()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var id = await SeedFaqEntryAsync(
            question: $"Hur öppnar jag ett sparkonto, {uniqueId}?",
            answer: $"Svar {uniqueId}",
            category: $"Kategori-{uniqueId}",
            keywords: "sparkonto,öppna");

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/faqs/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FaqEntryResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(id);
        body.Question.Should().Be($"Hur öppnar jag ett sparkonto, {uniqueId}?");
        body.Category.Should().Be($"Kategori-{uniqueId}");
    }

    [PostgresFact]
    public async Task GetById_UnknownId_Returns404NotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/faqs/999999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [PostgresFact]
    public async Task Search_ByCategory_ReturnsOnlyMatchingEntriesFromDatabase()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var matchingCategory = $"Sparande-{uniqueId}";
        var matchingId = await SeedFaqEntryAsync(
            question: $"Vad är ränta, {uniqueId}?",
            answer: "Ersättning för sparande.",
            category: matchingCategory,
            keywords: "ränta");
        var otherId = await SeedFaqEntryAsync(
            question: $"Hur byter jag kort, {uniqueId}?",
            answer: "Beställ nytt kort i appen.",
            category: $"Kort-{uniqueId}",
            keywords: "kort");

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/faqs/search?category={Uri.EscapeDataString(matchingCategory)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<FaqEntryResponse>>();
        results.Should().NotBeNull();
        results!.Select(r => r.Id).Should().Contain(matchingId);
        results.Select(r => r.Id).Should().NotContain(otherId);
    }

    [PostgresFact]
    public async Task Create_WithFaqManagePermission_PersistsEntryToDatabase()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateFaqManageToken());

        var response = await client.PostAsJsonAsync("/api/faqs", new CreateFaqRequest(
            Question: $"Kan jag pausa mitt sparande, {uniqueId}?",
            Answer: "Ja, det går att pausa när som helst.",
            Category: $"Sparande-{uniqueId}",
            Keywords: "pausa,sparande"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<FaqCreatedResponse>();
        created.Should().NotBeNull();
        _createdFaqIds.Add(created!.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FaqDbContext>();
        var stored = await db.FaqEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == created.Id);

        stored.Should().NotBeNull();
        stored!.Question.Should().Be($"Kan jag pausa mitt sparande, {uniqueId}?");
        stored.Category.Should().Be($"Sparande-{uniqueId}");
    }

    [PostgresFact]
    public async Task Create_WithoutFaqManagePermission_Returns403Forbidden()
    {
        var client = await CreateLoggedInRegularCustomerClientAsync();

        var response = await client.PostAsJsonAsync("/api/faqs", new CreateFaqRequest(
            Question: "En fråga som en vanlig kund inte får skapa?",
            Answer: "Svar."));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [PostgresFact]
    public async Task Create_WithoutAuthentication_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/faqs", new CreateFaqRequest(
            Question: "En fråga utan inloggning?",
            Answer: "Svar."));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [PostgresFact]
    public async Task Delete_WithFaqManagePermission_RemovesEntryFromDatabase()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var id = await SeedFaqEntryAsync(
            question: $"Fråga att ta bort, {uniqueId}?",
            answer: "Svar.",
            category: null,
            keywords: null);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateFaqManageToken());

        var response = await client.DeleteAsync($"/api/faqs/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FaqDbContext>();
        var stored = await db.FaqEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id);
        stored.Should().BeNull();
    }

    [PostgresFact]
    public async Task Delete_UnknownId_Returns404NotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateFaqManageToken());

        var response = await client.DeleteAsync("/api/faqs/999999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // Remove everything this test class created so the shared CI database stays clean
    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<FaqDbContext>();
        var entries = await db.FaqEntries.Where(e => _createdFaqIds.Contains(e.Id)).ToListAsync();
        db.FaqEntries.RemoveRange(entries);
        await db.SaveChangesAsync();

        await PostgresTestData.DeleteCustomersAsync(_factory.Services, _createdCustomerIds);
    }

    private async Task<int> SeedFaqEntryAsync(string question, string answer, string? category, string? keywords)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FaqDbContext>();

        var entry = FaqEntry.Create(question, answer, category, keywords);
        db.FaqEntries.Add(entry);
        await db.SaveChangesAsync();

        _createdFaqIds.Add(entry.Id);
        return entry.Id;
    }

    // JwtProvider never issues a "permission" claim through real login, so faq:manage can only be
    // reached by minting a token the same way JwtProvider does, with that claim added.
    private string CreateFaqManageToken()
    {
        using var scope = _factory.Services.CreateScope();
        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Random.Shared.Next(1000, 999999).ToString()),
            new(JwtRegisteredClaimNames.Email, "faq-manager@exempel.se"),
            new(ClaimTypes.Role, "Customer"),
            new("permission", "faq:manage")
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

    private async Task<HttpClient> CreateLoggedInRegularCustomerClientAsync()
    {
        var customer = await PostgresTestData.CreateCustomerAsync(_factory.Services, "faq");
        _createdCustomerIds.Add(customer.Id);

        return await PostgresTestData.CreateLoggedInClientAsync(_factory, customer);
    }
}
