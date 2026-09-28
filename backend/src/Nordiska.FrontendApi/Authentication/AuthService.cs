using System.Collections.Concurrent;
using ActiveLogin.Authentication.BankId.Api;
using ActiveLogin.Authentication.BankId.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nordiska.FrontendApi.Authentication;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;



namespace Nordiska.Modules.Banking.Application;

public class AuthService : IAuthService
{
    private static readonly ConcurrentDictionary<string, string> _simulatedOrderPersonalNumbers = new();

    // Only accepted in Development and only for customers without a password (seeded demo users)
    private const string DevelopmentPassword = "password123";

    private readonly IBankIdAppApiClient _bankIdAppApiClient;
    private readonly IJwtProvider _jwtProvider;
    private readonly JwtOptions _jwtOptions;
    private readonly UserManager<Customer> _userManager;
    private readonly BankingDbContext _db;
    private readonly IHostEnvironment _environment;

    public AuthService(
        BankingDbContext db,
        UserManager <Customer> userManager,
        IBankIdAppApiClient bankIdAppApiClient,
        IJwtProvider jwtProvider,
        IOptions<JwtOptions> jwtOptions,
        IHostEnvironment environment)
    {
        _bankIdAppApiClient = bankIdAppApiClient;
        _jwtProvider = jwtProvider;
        _jwtOptions = jwtOptions.Value;
        _userManager = userManager;
        _db = db;
        _environment = environment;
    }

    public async Task<AuthenticationResultDto> InitiateBankIdAsync(BankIdInitiateRequest request, string clientIp)
    {
        try
        {
            var cleanPersonalNum = request?.PersonalNum?.Replace("-", "")?.Trim();

            var requirement = !string.IsNullOrWhiteSpace(cleanPersonalNum)
                ? new Requirement(personalNumber: cleanPersonalNum)
                : null;

            var effectiveIp = string.IsNullOrWhiteSpace(clientIp) || clientIp == "::1" ? "127.0.0.1" : clientIp;

            var response = await _bankIdAppApiClient.AuthAsync(new AuthRequest(
                endUserIp: effectiveIp,
                requirement: requirement
            ));

            if (!string.IsNullOrWhiteSpace(cleanPersonalNum))
            {
                _simulatedOrderPersonalNumbers[response.OrderRef] = cleanPersonalNum;
            }

            var initiateData = new BankIdInitiateResponseDto(
                response.OrderRef,
                response.AutoStartToken,
                response.QrStartToken,
                response.QrStartSecret
            );

            return new AuthenticationResultDto(true, null, InitiateData: initiateData);
        }
        catch (BankIdApiException ex)
        {
            return new AuthenticationResultDto(false, $"BankID API error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            return new AuthenticationResultDto(false, $"Could not start BankID: {ex.Message}");
        }
    }

    public async Task<AuthenticationResultDto> CollectBankIdAsync(BankIdCollectRequest request, HttpResponse response)
    {
        try
        {
            var collectResponse = await _bankIdAppApiClient.CollectAsync(new CollectRequest(request.OrderRef));

            if (!string.Equals(collectResponse.Status, nameof(CollectStatus.Complete), StringComparison.OrdinalIgnoreCase))
            {
                var pendingData = new BankIdCollectResponseDto(
                    collectResponse.Status.ToString().ToUpper(),
                    collectResponse.HintCode.ToString(),
                    null
                );

                return new AuthenticationResultDto(true, null, CollectData: pendingData);
            }

            string cleanPersonalNumber;
            if (_simulatedOrderPersonalNumbers.TryRemove(request.OrderRef, out var initiatedPersonalNum))
            {
                cleanPersonalNumber = initiatedPersonalNum;
            }
            else
            {
                var rawPersonalNumber = collectResponse.CompletionData?.User.PersonalIdentityNumber ?? string.Empty;
                cleanPersonalNumber = rawPersonalNumber.Replace("-", "").Trim();
            }
            
            if (string.IsNullOrEmpty(cleanPersonalNumber))
            {
                return new AuthenticationResultDto(false, "Personal number missing from BankID completion data.");
            }
            
            var customer = await _db.Customers.FirstOrDefaultAsync(c => 
                c.PersonalNum == cleanPersonalNumber);

            if (customer == null)
            {
                return new AuthenticationResultDto(false, $"Could not find customer with personal number: '{cleanPersonalNumber}'.");
            }

            // BankID is strong authentication, so it lifts a password lockout (NOR-70).
            // Otherwise an attacker could keep a customer locked out by guessing every 15 minutes.
            await _userManager.ResetAccessFailedCountAsync(customer);
            await _userManager.SetLockoutEndDateAsync(customer, null);

            var token = await _jwtProvider.Generate(customer);
            response.AppendAuthCookie(token, _jwtOptions.TokenLifetimeInMinutes);

            var completeData = new BankIdCollectResponseDto(
                "COMPLETE",
                null,
                new CustomerResponseDto(customer.Id, customer.Email ?? string.Empty, customer.Name)
            );

            return new AuthenticationResultDto(true, null, Token: token, CollectData: completeData);
        }
        catch (BankIdApiException ex)
        {
            return new AuthenticationResultDto(false, $"BankID API Error: {ex.ErrorCode}");
        }
    }

    public async Task<AuthenticationResultDto> RegisterCustomerAsync(RegisterCustomerRequestDto request, HttpResponse response)
    {
        var existingCustomer = await _userManager.FindByEmailAsync(request.Email);
        if (existingCustomer != null)
        {
            return new AuthenticationResultDto(false, "En användare med denna e-post finns redan.");
        }

        var newCustomer = new Customer
        {
            UserName = request.Email,
            Name = request.Name,
            PersonalNum = request.PersonalNum,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(newCustomer);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return new AuthenticationResultDto(false, "Registreringen misslyckades.", Errors: errors);
        }

        var token = await _jwtProvider.Generate(newCustomer);
        response.AppendAuthCookie(token, _jwtOptions.TokenLifetimeInMinutes);
        return new AuthenticationResultDto(true, null, Token: token);
    }

    public async Task<AuthenticationResultDto> LoginAsync(LoginRequest request, HttpResponse response)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request?.Password))
        {
            return new AuthenticationResultDto(false, "E-post och lösenord krävs.");
        }

        // UserManager looks up on the normalized columns, so this is already case-insensitive
        var login = request.Email.Trim();
        var customer = await _userManager.FindByEmailAsync(login) ?? await _userManager.FindByNameAsync(login);

        if (customer == null)
        {
            return InvalidCredentials();
        }

        if (await _userManager.IsLockedOutAsync(customer))
        {
            return LockedOut(customer);
        }

        if (!await IsPasswordValidAsync(customer, request.Password))
        {
            // Counts the failure and locks the account when MaxFailedAccessAttempts is reached
            await _userManager.AccessFailedAsync(customer);

            return await _userManager.IsLockedOutAsync(customer)
                ? LockedOut(customer)
                : InvalidCredentials();
        }

        await _userManager.ResetAccessFailedCountAsync(customer);

        var token = await _jwtProvider.Generate(customer);
        response.AppendAuthCookie(token, _jwtOptions.TokenLifetimeInMinutes);

        var customerDto = new CustomerResponseDto(customer.Id, customer.Email ?? string.Empty, customer.Name, token);
        var completeData = new BankIdCollectResponseDto("COMPLETE", null, customerDto);

        return new AuthenticationResultDto(true, null, Token: token, CollectData: completeData);
    }

    public async Task<AuthenticationResultDto> RefreshSessionAsync(string customerIdOrEmail, HttpResponse response)
    {
        if (string.IsNullOrWhiteSpace(customerIdOrEmail))
        {
            return new AuthenticationResultDto(false, "Ogiltig session.");
        }

        Customer? customer = null;
        if (long.TryParse(customerIdOrEmail, out var customerId))
        {
            customer = await _userManager.FindByIdAsync(customerIdOrEmail) 
                       ?? await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        }

        if (customer == null)
        {
            var normalized = customerIdOrEmail.Trim();
            customer = await _userManager.FindByEmailAsync(normalized)
                       ?? await _userManager.FindByNameAsync(normalized)
                       ?? await _db.Customers.FirstOrDefaultAsync(c => c.Email == normalized);
        }

        if (customer == null)
        {
            return new AuthenticationResultDto(false, "Användaren hittades inte.");
        }

        if (await _userManager.IsLockedOutAsync(customer))
        {
            return LockedOut(customer);
        }

        var token = await _jwtProvider.Generate(customer);
        response.AppendAuthCookie(token, _jwtOptions.TokenLifetimeInMinutes);

        var customerDto = new CustomerResponseDto(customer.Id, customer.Email ?? string.Empty, customer.Name);
        var completeData = new BankIdCollectResponseDto("COMPLETE", null, customerDto);

        return new AuthenticationResultDto(true, null, Token: token, CollectData: completeData);
    }

    private async Task<bool> IsPasswordValidAsync(Customer customer, string password)
    {
        if (!string.IsNullOrEmpty(customer.PasswordHash))
        {
            return await _userManager.CheckPasswordAsync(customer, password);
        }

        // Seeded demo customers have no password, so keep the dev password for local development only
        return _environment.IsDevelopment() && password == DevelopmentPassword;
    }

    private static AuthenticationResultDto InvalidCredentials()
        => new(false, "Felaktig e-post eller lösenord.", FailureReason: AuthFailureReason.InvalidCredentials);

    private static AuthenticationResultDto LockedOut(Customer customer)
        => new(false, "Kontot är tillfälligt spärrat.", FailureReason: AuthFailureReason.LockedOut, LockoutEnd: customer.LockoutEnd);
}