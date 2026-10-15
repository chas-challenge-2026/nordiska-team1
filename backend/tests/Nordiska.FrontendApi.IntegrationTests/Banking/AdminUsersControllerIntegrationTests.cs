using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.Contracts.Responses;
using Nordiska.FrontendApi.IntegrationTests.Authentication;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Banking;

public class AdminUsersWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<Microsoft.EntityFrameworkCore.DbContextOptions<BankingDbContext>>();
            services.RemoveAll<BankingDbContext>();
            services.AddDbContext<BankingDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}

public class AdminUsersControllerIntegrationTests : IClassFixture<AdminUsersWebApplicationFactory>
{
    private readonly AdminUsersWebApplicationFactory _factory;

    public AdminUsersControllerIntegrationTests(AdminUsersWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonymousClient() => _factory.CreateClient();

    private HttpClient CreateCustomerClient(long customerId = 1)
    {
        var client = _factory.CreateClient();
        var token = GenerateToken(customerId, "customer@example.com", "Customer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient CreateAdminClient(long adminId = 9999)
    {
        var client = _factory.CreateClient();
        var token = GenerateToken(adminId, "admin@nordiska.se", "Admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string GenerateToken(long id, string email, string role)
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var secretKey = config["Jwt:SecretKey"] ?? "DEVELOPMENT_PLACEHOLDER_OVERRIDDEN_BY_USER_SECRETS";
        var issuer = config["Jwt:Issuer"] ?? "NordiskaSparbanken";
        var audience = config["Jwt:Audience"] ?? "NordiskaSparbankenPortal";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GetAll_WithoutToken_Returns_401Unauthorized()
    {
        var client = CreateAnonymousClient();
        var response = await client.GetAsync("/api/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_AsCustomer_Returns_403Forbidden()
    {
        var client = CreateCustomerClient();
        var response = await client.GetAsync("/api/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_AsAdmin_Returns_SuccessAndListsAdmins()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<List<AdminUserResponse>>();
        users.Should().NotBeNull();
        users!.Should().Contain(u => u.Email == "admin@nordiska.se");
    }

    [Fact]
    public async Task Create_AdminUser_Succeeds_AndCanBeRetrievedAndDeleted()
    {
        var client = CreateAdminClient(adminId: 8888);
        var uniqueEmail = $"newadmin_{Guid.NewGuid():N}@nordiska.se";

        var createRequest = new CreateAdminUserRequest
        {
            Email = uniqueEmail,
            Name = "Ny Handläggare",
            Password = "SecurePassword123!",
            Role = "BankStaff",
            PhoneNumber = "+46701122334"
        };

        // 1. Create
        var createResponse = await client.PostAsJsonAsync("/api/admin/users", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdUser = await createResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be(uniqueEmail);
        createdUser.Name.Should().Be("Ny Handläggare");
        createdUser.Roles.Should().Contain("BankStaff");
        createdUser.PersonalNum.Should().NotBeNullOrWhiteSpace();

        // 2. GetById
        var getResponse = await client.GetAsync($"/api/admin/users/{createdUser.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrievedUser = await getResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        retrievedUser!.Email.Should().Be(uniqueEmail);

        // 3. Update
        var updateRequest = new UpdateAdminUserRequest
        {
            Name = "Uppdaterat Namn",
            Role = "Admin"
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/admin/users/{createdUser.Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedUser = await updateResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        updatedUser!.Name.Should().Be("Uppdaterat Namn");
        updatedUser.Roles.Should().Contain("Admin");

        // 4. Change Password
        var changePasswordRequest = new ChangeAdminPasswordRequest { NewPassword = "BrandNewPassword456!" };
        var pwResponse = await client.PutAsJsonAsync($"/api/admin/users/{createdUser.Id}/password", changePasswordRequest);
        pwResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Delete
        var deleteResponse = await client.DeleteAsync($"/api/admin/users/{createdUser.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Verify Deleted
        var verifyResponse = await client.GetAsync($"/api/admin/users/{createdUser.Id}");
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateEmail_Returns_409Conflict()
    {
        var client = CreateAdminClient();
        var createRequest = new CreateAdminUserRequest
        {
            Email = "admin@nordiska.se", // Already exists
            Name = "Duplicate Admin",
            Password = "Password123!"
        };

        var response = await client.PostAsJsonAsync("/api/admin/users", createRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_Self_Returns_400BadRequest()
    {
        var adminId = 9999;
        var client = CreateAdminClient(adminId: adminId);

        var response = await client.DeleteAsync($"/api/admin/users/{adminId}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}