using System;
using System.Collections.Generic;
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
    public async Task Create_WithoutAuthentication_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/savings-goals",
            new CreateSavingsGoalRequest(1, "Buffert", 10000m));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ValidGoal_Returns201Created()
    {
        var client = await CreateAuthenticatedClientAsync();

        var request = new CreateSavingsGoalRequest(
            AccountId: 1,
            Title: "Drömresan 2027",
            TargetAmount: 25000m,
            TargetDate: DateTime.UtcNow.AddMonths(12)
        );

        var response = await client.PostAsJsonAsync("/api/savings-goals", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<SavingsGoalResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Drömresan 2027");
        body.TargetAmount.Should().Be(25000m);
        body.CurrentAmount.Should().Be(0m);
        body.Status.Should().Be("active");
        body.AccountId.Should().Be(1);
    }

    [Fact]
    public async Task Create_InvalidTitleOrAmount_Returns400BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();

        var emptyTitleReq = new CreateSavingsGoalRequest(1, "", 10000m);
        var resp1 = await client.PostAsJsonAsync("/api/savings-goals", emptyTitleReq);
        resp1.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var negativeAmountReq = new CreateSavingsGoalRequest(1, "Giltig titel", -500m);
        var resp2 = await client.PostAsJsonAsync("/api/savings-goals", negativeAmountReq);
        resp2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetGoals_Returns200WithCustomerGoals()
    {
        var client = await CreateAuthenticatedClientAsync();

        TestSavingsGoalRepository.Seed(new SavingsGoal
        {
            Id = 601,
            CustomerId = 1,
            AccountId = 1,
            Title = "Konto 1 Mål",
            TargetAmount = 5000m,
            CurrentAmount = 1250m,
            Status = "active"
        });
        TestSavingsGoalRepository.Seed(new SavingsGoal
        {
            Id = 612,
            CustomerId = 1,
            AccountId = 2,
            Title = "Konto 2 Mål",
            TargetAmount = 10000m,
            CurrentAmount = 2500m,
            Status = "active"
        });

        var response = await client.GetAsync("/api/savings-goals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var overview = await response.Content.ReadFromJsonAsync<SavingsGoalsOverviewResponse>();
        overview.Should().NotBeNull();
        overview!.Goals.Should().Contain(g => g.Id == 601 && g.Title == "Konto 1 Mål");
        overview.Goals.Should().Contain(g => g.Id == 612 && g.Title == "Konto 2 Mål");
        overview.Summary.TotalCurrentBalance.Should().Be(3750m);
        overview.Summary.TotalTargetAmount.Should().Be(15000m);
        overview.Summary.TotalProgressPercentage.Should().Be(25m);
    }

    [Fact]
    public async Task GetGoals_ByAccount_ReturnsOnlyThatAccountsCustomerGoals()
    {
        var client = await CreateAuthenticatedClientAsync();

        TestSavingsGoalRepository.Seed(new SavingsGoal
        {
            Id = 611,
            CustomerId = 1,
            AccountId = 1,
            Title = "Konto 1 Mål",
            TargetAmount = 5000m,
            Status = "active"
        });

        var response = await client.GetAsync("/api/accounts/1/savings-goals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var goals = await response.Content.ReadFromJsonAsync<List<SavingsGoalResponse>>();
        goals.Should().NotBeNull();
        goals.Should().ContainSingle(goal => goal.Id == 611);
    }

    [Fact]
    public async Task GetGoals_ByAnotherCustomersAccount_ReturnsEmptyList()
    {
        var client = await CreateAuthenticatedClientAsync();

        TestSavingsGoalRepository.Seed(new SavingsGoal
        {
            Id = 613,
            CustomerId = 2,
            AccountId = 3,
            Title = "Other customer's goal",
            TargetAmount = 5000m,
            Status = "active"
        });

        var response = await client.GetAsync("/api/accounts/3/savings-goals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var goals = await response.Content.ReadFromJsonAsync<List<SavingsGoalResponse>>();
        goals.Should().NotBeNull();
        goals.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingGoal_Returns200()
    {
        var client = await CreateAuthenticatedClientAsync();

        TestSavingsGoalRepository.Seed(new SavingsGoal
        {
            Id = 602,
            CustomerId = 1,
            AccountId = 1,
            Title = "Detaljvy Mål",
            TargetAmount = 8000m,
            Status = "active"
        });

        var response = await client.GetAsync("/api/savings-goals/602");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SavingsGoalResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(602);
        body.Title.Should().Be("Detaljvy Mål");
    }

    [Fact]
    public async Task GetById_NonExistentGoal_Returns404NotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/savings-goals/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ValidPatch_Returns200WithUpdatedFields()
    {
        var client = await CreateAuthenticatedClientAsync();

        TestSavingsGoalRepository.Seed(new SavingsGoal
        {
            Id = 603,
            CustomerId = 1,
            AccountId = 1,
            Title = "Gammal titel",
            TargetAmount = 5000m,
            Status = "active"
        });

        var patchReq = new UpdateSavingsGoalRequest(
            Title: "Ny uppdaterad titel",
            TargetAmount: 7000m,
            Status: "paused"
        );

        var response = await client.PatchAsJsonAsync("/api/savings-goals/603", patchReq);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SavingsGoalResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Ny uppdaterad titel");
        body.TargetAmount.Should().Be(7000m);
        body.Status.Should().Be("paused");
    }

    [Fact]
    public async Task Update_InvalidStatus_Returns400BadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();

        TestSavingsGoalRepository.Seed(new SavingsGoal
        {
            Id = 604,
            CustomerId = 1,
            AccountId = 1,
            Title = "Titel",
            TargetAmount = 5000m,
            Status = "active"
        });

        var patchReq = new UpdateSavingsGoalRequest(Status: "ogiltig_status");

        var response = await client.PatchAsJsonAsync("/api/savings-goals/604", patchReq);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_ValidGoal_Returns204NoContent()
    {
        var client = await CreateAuthenticatedClientAsync();

        TestSavingsGoalRepository.Seed(new SavingsGoal
        {
            Id = 605,
            CustomerId = 1,
            AccountId = 1,
            Title = "Radera mig",
            TargetAmount = 5000m,
            Status = "active"
        });

        var response = await client.DeleteAsync("/api/savings-goals/605");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it was deleted
        var getResp = await client.GetAsync("/api/savings-goals/605");
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

        var request = new AutomateSavingsGoalRequest(
            SourceAccountId: 3,
            MonthlyAmount: 500m,
            DayOfMonth: 15
        );

        var response = await client.PostAsJsonAsync($"/api/savings-goals/{goal.Id}/automate", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
