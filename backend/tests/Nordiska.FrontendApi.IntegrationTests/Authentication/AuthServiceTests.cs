using System;
using System.Threading.Tasks;
using ActiveLogin.Authentication.BankId.Api;
using ActiveLogin.Authentication.BankId.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Nordiska.FrontendApi.Authentication;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Authentication;

public sealed class AuthServiceTests : IDisposable
{
    private readonly BankingDbContext _db;
    private readonly Mock<UserManager<Customer>> _userManagerMock;
    private readonly Mock<IBankIdAppApiClient> _bankIdApiClientMock;
    private readonly Mock<IJwtProvider> _jwtProviderMock;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        // Use a unique database for each test instance to prevent state leakage.
        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new BankingDbContext(options);

        var userStoreMock = new Mock<IUserStore<Customer>>();
        _userManagerMock = new Mock<UserManager<Customer>>(
            userStoreMock.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        _bankIdApiClientMock = new Mock<IBankIdAppApiClient>();
        _jwtProviderMock = new Mock<IJwtProvider>();

        _sut = new AuthService(
            _db,
            _userManagerMock.Object,
            _bankIdApiClientMock.Object,
            _jwtProviderMock.Object,
            Options.Create(new JwtOptions
            {
                SecretKey = "super-secret-key-for-testing-purposes",
                TokenLifetimeInMinutes = 60
            }));
    }

    public void Dispose()
    {
        // Release the in-memory database after each test instance.
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task InitiateBankIdAsync_WhenSuccessful_ReturnsInitiateData()
    {
        // Arrange
        _bankIdApiClientMock
            .Setup(x => x.AuthAsync(It.IsAny<AuthRequest>()))
            .ReturnsAsync(new AuthResponse("order-initiate", "auto-start", "qr-start", "qr-secret"));

        // Act
        var result = await _sut.InitiateBankIdAsync(
            new BankIdInitiateRequest("198202116050"),
            "127.0.0.1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.InitiateData.Should().BeEquivalentTo(new
        {
            OrderRef = "order-initiate",
            AutoStartToken = "auto-start",
            QrStartToken = "qr-start",
            QrStartSecret = "qr-secret"
        });
    }

    [Fact]
    public async Task CollectBankIdAsync_WhenPending_ReturnsPendingData()
    {
        // Arrange
        _bankIdApiClientMock
            .Setup(x => x.CollectAsync(It.IsAny<CollectRequest>()))
            .ReturnsAsync(new CollectResponse(
                "order-pending",
                "PENDING",
                "OutstandingTransaction"));

        // Act
        var result = await _sut.CollectBankIdAsync(
            new BankIdCollectRequest("order-pending"),
            new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.CollectData.Should().NotBeNull();
        result.CollectData!.Status.Should().Be("PENDING");
        result.CollectData.HintCode.Should().Be("OutstandingTransaction");
        result.CollectData.Customer.Should().BeNull();
    }

    [Fact]
    public async Task CollectBankIdAsync_WhenCompletionHasNoPersonalNumber_ReturnsFailure()
    {
        // Arrange
        _bankIdApiClientMock
            .Setup(x => x.CollectAsync(It.IsAny<CollectRequest>()))
            .ReturnsAsync(new CollectResponse(
                "order-missing-personal-number",
                "COMPLETE",
                string.Empty));

        // Act
        var result = await _sut.CollectBankIdAsync(
            new BankIdCollectRequest("order-missing-personal-number"),
            new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Personal number missing from BankID completion data.");
    }

    [Fact]
    public async Task CollectBankIdAsync_WhenCustomerExists_ReturnsTokenAndCustomer()
    {
        // Arrange
        const string personalNumber = "198202116050";
        await AddCustomerAsync(personalNumber, "Anna Svensson", "anna@nordiska.se");

        _bankIdApiClientMock
            .Setup(x => x.CollectAsync(It.IsAny<CollectRequest>()))
            .ReturnsAsync(new CollectResponse(
                "order-complete",
                "COMPLETE",
                string.Empty,
                CreateCompletionData(personalNumber)));
        _jwtProviderMock
            .Setup(x => x.Generate(It.IsAny<Customer>()))
            .ReturnsAsync("bankid-jwt");

        // Act
        var result = await _sut.CollectBankIdAsync(
            new BankIdCollectRequest("order-complete"),
            new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Token.Should().Be("bankid-jwt");
        result.CollectData!.Status.Should().Be("COMPLETE");
        result.CollectData.Customer!.Name.Should().Be("Anna Svensson");
    }

    [Fact]
    public async Task CollectBankIdAsync_WhenCustomerDoesNotExist_ReturnsFailure()
    {
        // Arrange
        const string personalNumber = "198202116050";
        _bankIdApiClientMock
            .Setup(x => x.CollectAsync(It.IsAny<CollectRequest>()))
            .ReturnsAsync(new CollectResponse(
                "order-unknown-customer",
                "COMPLETE",
                string.Empty,
                CreateCompletionData(personalNumber)));

        // Act
        var result = await _sut.CollectBankIdAsync(
            new BankIdCollectRequest("order-unknown-customer"),
            new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(
            $"Could not find customer with personal number: '{personalNumber}'.");
    }

    [Fact]
    public async Task RegisterCustomerAsync_WhenEmailExists_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterCustomerRequestDto(
            "198202116050", "Anna", "existing@nordiska.se", "0701234567");
        _userManagerMock
            .Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(new Customer { Email = request.Email });

        // Act
        var result = await _sut.RegisterCustomerAsync(request, new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("En användare med denna e-post finns redan.");
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCustomerAsync_WhenIdentityCreationFails_ReturnsErrors()
    {
        // Arrange
        var request = new RegisterCustomerRequestDto(
            "198202116050", "Anna", "anna@nordiska.se", "0701234567");
        _userManagerMock
            .Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((Customer?)null);
        _userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<Customer>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email is invalid." }));

        // Act
        var result = await _sut.RegisterCustomerAsync(request, new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Registreringen misslyckades.");
        result.Errors.Should().ContainSingle().Which.Should().Be("Email is invalid.");
    }

    [Fact]
    public async Task RegisterCustomerAsync_WhenSuccessful_ReturnsToken()
    {
        // Arrange
        var request = new RegisterCustomerRequestDto(
            "198202116050", "Anna", "anna@nordiska.se", "0701234567");
        _userManagerMock
            .Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((Customer?)null);
        _userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<Customer>()))
            .ReturnsAsync(IdentityResult.Success);
        _jwtProviderMock
            .Setup(x => x.Generate(It.IsAny<Customer>()))
            .ReturnsAsync("registration-jwt");

        // Act
        var result = await _sut.RegisterCustomerAsync(request, new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Token.Should().Be("registration-jwt");
        _userManagerMock.Verify(x => x.CreateAsync(It.Is<Customer>(customer =>
            customer.Email == request.Email &&
            customer.UserName == request.Email &&
            customer.PersonalNum == request.PersonalNum)), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreMissing_ReturnsFailure()
    {
        // Act
        var result = await _sut.LoginAsync(
            new LoginRequest("", ""),
            new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("E-post och lösenord krävs.");
    }

    [Fact]
    public async Task LoginAsync_WhenCustomerDoesNotExist_ReturnsFailure()
    {
        // Act
        var result = await _sut.LoginAsync(
            new LoginRequest("unknown@nordiska.se", "password123"),
            new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Felaktig e-post eller lösenord.");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsInvalid_ReturnsFailure()
    {
        // Arrange
        const string email = "user@nordiska.se";
        await AddCustomerAsync(email, "Erik Svensson", email, "hashed-password");
        _userManagerMock
            .Setup(x => x.CheckPasswordAsync(It.IsAny<Customer>(), "wrong-password"))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.LoginAsync(
            new LoginRequest(email, "wrong-password"),
            new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Felaktig e-post eller lösenord.");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsValid_ReturnsTokenAndCustomer()
    {
        // Arrange
        const string email = "user@nordiska.se";
        var customer = await AddCustomerAsync(email, "Erik Svensson", email, "hashed-password");
        _userManagerMock
            .Setup(x => x.CheckPasswordAsync(customer, "correct-password"))
            .ReturnsAsync(true);
        _jwtProviderMock
            .Setup(x => x.Generate(customer))
            .ReturnsAsync("login-jwt");

        // Act
        var result = await _sut.LoginAsync(
            new LoginRequest(email.ToUpperInvariant(), "correct-password"),
            new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Token.Should().Be("login-jwt");
        result.CollectData!.Customer!.Name.Should().Be("Erik Svensson");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordHashIsMissing_AcceptsDemoPassword()
    {
        // Arrange
        const string email = "demo@nordiska.se";
        await AddCustomerAsync(email, "Demo Customer", email);
        _jwtProviderMock
            .Setup(x => x.Generate(It.IsAny<Customer>()))
            .ReturnsAsync("demo-jwt");

        // Act
        var result = await _sut.LoginAsync(
            new LoginRequest(email, "password123"),
            new DefaultHttpContext().Response);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Token.Should().Be("demo-jwt");
    }

    private async Task<Customer> AddCustomerAsync(
        string personalNumber,
        string name,
        string email,
        string? passwordHash = null)
    {
        var customer = new Customer
        {
            PersonalNum = personalNumber,
            Name = name,
            Email = email,
            UserName = email,
            PasswordHash = passwordHash
        };

        await _db.Customers.AddAsync(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    private static CompletionData CreateCompletionData(string personalNumber)
    {
        return new CompletionData(
            user: new User(personalNumber, "Anna Svensson", "Anna", "Svensson"),
            device: new Device("127.0.0.1", "test-uhi"),
            bankIdIssueDate: DateTime.UtcNow.ToString("O"),
            stepUp: null,
            signature: "dummy-signature",
            ocspResponse: "dummy-ocsp-response",
            risk: null);
    }
}