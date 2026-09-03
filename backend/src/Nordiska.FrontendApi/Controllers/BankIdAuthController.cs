using ActiveLogin.Authentication.BankId.Api;
using ActiveLogin.Authentication.BankId.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;


namespace Nordiska.FrontendApi.Controllers;

[ApiController]
[Route("api/auth/bankid")]
public class BankIdAuthController : ControllerBase
{
    // ActiveLogins client to communicate with BankID 
     private readonly IBankIdAppApiClient _bankIdAppApiClient;
     //private readonly ApplicationDbContext _db; // TODO (When DB i set add ApplicationDbContext _db to the constructor )
     private readonly IJwtProvider _jwtProvider;

     public BankIdAuthController(IBankIdAppApiClient bankIdAppApiClient,
         IJwtProvider jwtProvider)
     {
         _bankIdAppApiClient = bankIdAppApiClient;
         _jwtProvider = jwtProvider;
     }
     
     // Log in 
     [HttpPost("Initiate")]
     public async Task<IActionResult> Initiate([FromBody] BankIdInitiateRequest request)
     {
         
         // Get client IP-adress (Required by BankId). 127.0.0.1 is for when run local
         var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
         
         try
         {
             // Create auth request to /Auth with client IP and personal number
             var response = await _bankIdAppApiClient.AuthAsync(new AuthRequest(
                endUserIp: clientIp,
                requirement: new Requirement([request.PersonalNum])
             ));
             
             // response 
             return Ok(new
             {
                 orderRef = response.OrderRef,
                 autoStartToken =  response.AutoStartToken,
                 qrStartToken = response.QrStartToken,
                 qrStartSecret = response.QrStartSecret
                 
             });
         }
         catch (BankIdApiException ex)
         {
             return BadRequest(new  {message = "Could not start BankID api", error = ex.Message});
         }
     }
     
     // we use /collect to ask BankId how the login is going. 
     [HttpPost("Collect")]
     public async Task<IActionResult> Collect([FromBody] BankIdCollectRequest request)
     {

         try
         {
             // ActiveLogin takes care of the request to /collect 
             var collectResponse = await _bankIdAppApiClient.CollectAsync(new CollectRequest(request.OrderRef));

             if (!string.Equals(collectResponse.Status, nameof(CollectStatus.Complete),
                     StringComparison.OrdinalIgnoreCase))
                 return Ok(new
                 {
                     status = collectResponse.Status.ToString().ToUpper(),
                     hintCode = collectResponse.HintCode.ToString()
                 });
             // Get customers personal nummer 
             var personalNumber = collectResponse.CompletionData?.User.PersonalIdentityNumber;
             // when the customer has logged in with bankId, look up user in the databas
             // TODO: use the line below when DB is set up and remove mockad data
             //var customer = await _db.Customers.FirstOrDefaultAsync(c => c.person_num == personalNumber);
             
             // MOCKAD CUSTOMER 
             var customer = new Customer
             {
                 Id = 1L,
                 Name = collectResponse.CompletionData?.User.Name ?? "Anna Andersson",
                 PersonalNum = personalNumber ?? "199011109087",
                 Email = $"Anna@exempel.com",
                 CreatedAt = DateTime.Now,

             };

             if (customer == null)
             {
                 return Unauthorized(new {message = "Could not find customer"});
             }
                 
             // if customer is found generate jwt token
             var token = await _jwtProvider.Generate(customer);
             
             return Ok(new
             {
                 status = "COMPLETE",
                 token,
                 customer = new { id = customer.Id, email = customer.Email, name = customer.Name}
             });

             // send back status (ex PENDING) and hintCode (ex started or UserSign) to frontend.  
         }
         catch (BankIdApiException ex)
         {
           return BadRequest(new  {message = "Could not start BankID api", error = ex.ErrorCode.ToString()});
         }
     }
     
}