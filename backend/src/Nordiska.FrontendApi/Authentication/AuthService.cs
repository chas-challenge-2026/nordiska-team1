using ActiveLogin.Authentication.BankId.Api;
using ActiveLogin.Authentication.BankId.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    private readonly IBankIdAppApiClient _bankIdAppApiClient;
    private readonly IJwtProvider _jwtProvider;
    private readonly JwtOptions _jwtOptions;
    private readonly UserManager<Customer> _userManager;
    private readonly BankingDbContext _db;

    public AuthService(
        BankingDbContext db,
        UserManager <Customer> userManager,
        IBankIdAppApiClient bankIdAppApiClient,
        IJwtProvider jwtProvider,
        IOptions<JwtOptions> jwtOptions)
    {
        _bankIdAppApiClient = bankIdAppApiClient;
        _jwtProvider = jwtProvider;
        _jwtOptions = jwtOptions.Value;
        _userManager = userManager;
        _db = db;
    }

    public async Task<AuthenticationResultDto> InitiateBankIdAsync(BankIdInitiateRequest request, string clientIp)
    {
        try
        {
            var requirement = !string.IsNullOrWhiteSpace(request?.PersonalNum)
                ? new Requirement(personalNumber: request.PersonalNum)
                : null;

            var response = await _bankIdAppApiClient.AuthAsync(new AuthRequest(
                endUserIp: clientIp,
                requirement: requirement
            ));

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
            return new AuthenticationResultDto(false, $"Could not start BankID API: {ex.Message}");
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

            var rawPersonalNumber = collectResponse.CompletionData?.User.PersonalIdentityNumber ?? string.Empty;
            var cleanPersonalNumber = rawPersonalNumber.Replace("-", "").Trim();
            
            if (string.IsNullOrEmpty(cleanPersonalNumber))
            {
                return new AuthenticationResultDto(false, "Personal number missing from BankID completion data.");
            }
            
            var customer = await _db.Customers.FirstOrDefaultAsync(c => 
                c.PersonalNum == cleanPersonalNumber || 
                c.PersonalNum == rawPersonalNumber);

            if (customer == null)
            {
                return new AuthenticationResultDto(false, $"Could not find customer with personal number: '{cleanPersonalNumber}' (raw: '{rawPersonalNumber}').");
            }

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
}