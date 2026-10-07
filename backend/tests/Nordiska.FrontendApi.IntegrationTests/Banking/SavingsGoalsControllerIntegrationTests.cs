using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

public class SavingsGoalsControllerIntegrationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public SavingsGoalsControllerIntegrationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
        TestSavingsGoalRepository.Reset();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email = "anna@exempel.se")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "password123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    [Fact]
    public async Task Automate_WithoutAuthentication_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/savings-goals/1/automate",
            new AutomateSavingsGoalRequest(2, 500m, 25));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelAutomate_WithoutAuthentication_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/savings-goals/1/automate");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Automate_NonExistentGoal_Returns404NotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/savings-goals/999999/automate",
            new AutomateSavingsGoalRequest(2, 500m, 25));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelAutomate_NonExistentGoal_Returns404NotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync("/api/savings-goals/999999/automate");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AutomateAndCancel_ValidFlow_ReturnsOkAndNoContent()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Arrange test goal for Anna (CustomerId 1, AccountId 1)
        var goal = new SavingsGoal
        {
            Id = 555,
            AccountId = 1,
            CustomerId = 1,
            Title = "Semesterresa",
            TargetAmount = 15000m,
            CurrentAmount = 0m,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };
        TestSavingsGoalRepository.Seed(goal);

        // 2. Schedule automation (SourceAccountId 2: Lönekonto)
        var request = new AutomateSavingsGoalRequest(
            SourceAccountId: 2,
            MonthlyAmount: 750m,
            DayOfMonth: 27
        );

        var automateResponse = await client.PostAsJsonAsync($"/api/savings-goals/{goal.Id}/automate", request);
        automateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await automateResponse.Content.ReadFromJsonAsync<AutomateSavingsGoalResponse>();
        body.Should().NotBeNull();
        body!.SavingsGoalId.Should().Be(goal.Id);
        body.SourceAccountId.Should().Be(2);
        body.TargetAccountId.Should().Be(1);
        body.MonthlyAmount.Should().Be(750m);
        body.DayOfMonth.Should().Be(27);
        body.Status.Should().Be("active");

        // 3. Cancel automation
        var cancelResponse = await client.DeleteAsync($"/api/savings-goals/{goal.Id}/automate");
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Automate_SourceAccountBelongsToOtherCustomer_Returns400BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();

        var goal = new SavingsGoal
        {
            Id = 556,
            AccountId = 1,
            CustomerId = 1,
            Title = "Sparmål",
            TargetAmount = 5000m,
            Status = "active"
        };
        TestSavingsGoalRepository.Seed(goal);

        // Account 3 belongs to Erik (CustomerId 2)
        var request = new AutomateSavingsGoalRequest(
            SourceAccountId: 3,
            MonthlyAmount: 500m,
            DayOfMonth: 15
        );

        var response = await client.PostAsJsonAsync($"/api/savings-goals/{goal.Id}/automate", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
