using Microsoft.AspNetCore.Http;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.Contracts.Responses;


namespace Nordiska.Modules.Banking.Application;

public interface IAuthService
{ 
    Task<AuthenticationResultDto> InitiateBankIdAsync(BankIdInitiateRequest request, string clientIp);
    Task<AuthenticationResultDto> CollectBankIdAsync(BankIdCollectRequest request, HttpResponse response);
    Task<AuthenticationResultDto> RegisterCustomerAsync(RegisterCustomerRequestDto request);  
}