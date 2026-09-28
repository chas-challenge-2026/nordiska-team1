using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

public class OperationalMessagesControllerIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public OperationalMessagesControllerIntegrationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonymousClient() => _factory.CreateClient();

    private async Task<HttpClient> CreateCustomerClientAsync(string email = "anna@exempel.se")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "password123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var secretKey = config["Jwt:SecretKey"] ?? "DEVELOPMENT_PLACEHOLDER_OVERRIDDEN_BY_USER_SECRETS";
        var issuer = config["Jwt:Issuer"] ?? "NordiskaSparbanken";
        var audience = config["Jwt:Audience"] ?? "NordiskaSparbankenPortal";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "9999"),
            new Claim(JwtRegisteredClaimNames.Email, "admin@nordiska.se"),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
        return client;
    }

    [Fact]
    public async Task GetActive_PublicEndpoint_Returns_200Ok_With_BilingualContent()
    {
        var client = CreateAnonymousClient();
        var response = await client.GetAsync("/api/operational-messages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await response.Content.ReadFromJsonAsync<List<OperationalMessageResponse>>();

        messages.Should().NotBeNull();
        messages!.Should().NotBeEmpty();
        messages![0].Title.Sv.Should().NotBeNullOrWhiteSpace();
        messages[0].Title.En.Should().NotBeNullOrWhiteSpace();
        messages[0].Message.Sv.Should().NotBeNullOrWhiteSpace();
        messages[0].Message.En.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AdminGetAll_WithoutAuth_Returns_401Unauthorized()
    {
        var client = CreateAnonymousClient();
        var response = await client.GetAsync("/api/operational-messages/all");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminGetAll_WithCustomerRole_Returns_403Forbidden()
    {
        var client = await CreateCustomerClientAsync("anna@exempel.se");
        var response = await client.GetAsync("/api/operational-messages/all");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_Can_Perform_Full_Crud_Lifecycle()
    {
        var adminClient = CreateAdminClient();

        // 1. Create a new message
        var createRequest = new CreateOperationalMessageRequest(
            TitleSv: "Varning för planerade störningar",
            TitleEn: "Notice of planned disruptions",
            MessageSv: "Vi uppdaterar systemen mellan 01:00 och 03:00.",
            MessageEn: "We are updating systems between 01:00 and 03:00.",
            Severity: "warning",
            IsActive: true,
            Priority: 20,
            StartDate: DateTime.UtcNow.AddDays(-1),
            EndDate: DateTime.UtcNow.AddDays(5)
        );

        var createResponse = await adminClient.PostAsJsonAsync("/api/operational-messages", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<OperationalMessageResponse>();
        created.Should().NotBeNull();
        created!.Id.Should().BeGreaterThan(0);
        created.Title.Sv.Should().Be("Varning för planerade störningar");
        created.Title.En.Should().Be("Notice of planned disruptions");
        created.Severity.Should().Be("warning");
        created.Priority.Should().Be(20);
        created.IsActive.Should().BeTrue();

        var messageId = created.Id;

        // 2. Get by ID
        var getByIdResponse = await adminClient.GetAsync($"/api/operational-messages/{messageId}");
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<OperationalMessageResponse>();
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(messageId);

        // 3. Patch status (deactivate)
        var patchRequest = new PatchOperationalMessageRequest(IsActive: false, Priority: 5);
        var patchResponse = await adminClient.PatchAsJsonAsync($"/api/operational-messages/{messageId}", patchRequest);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await patchResponse.Content.ReadFromJsonAsync<OperationalMessageResponse>();
        patched.Should().NotBeNull();
        patched!.IsActive.Should().BeFalse();
        patched.Priority.Should().Be(5);

        // 4. Update (PUT)
        var updateRequest = new UpdateOperationalMessageRequest(
            TitleSv: "Uppdaterad varning",
            TitleEn: "Updated notice",
            MessageSv: "Ny information om driftstopp.",
            MessageEn: "New info on maintenance window.",
            Severity: "critical",
            IsActive: true,
            Priority: 30,
            StartDate: null,
            EndDate: null
        );

        var updateResponse = await adminClient.PutAsJsonAsync($"/api/operational-messages/{messageId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<OperationalMessageResponse>();
        updated.Should().NotBeNull();
        updated!.Title.Sv.Should().Be("Uppdaterad varning");
        updated.Severity.Should().Be("critical");
        updated.Priority.Should().Be(30);

        // 5. Delete
        var deleteResponse = await adminClient.DeleteAsync($"/api/operational-messages/{messageId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Verify deleted returns 404
        var getDeletedResponse = await adminClient.GetAsync($"/api/operational-messages/{messageId}");
        getDeletedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
