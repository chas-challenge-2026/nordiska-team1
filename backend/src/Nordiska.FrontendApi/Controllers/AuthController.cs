using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.Modules.Banking.Application;

namespace Nordiska.FrontendApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("bankid/initiate")]
    public async Task<IActionResult> Initiate([FromBody] BankIdInitiateRequest request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _authService.InitiateBankIdAsync(request, clientIp);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result.InitiateData);
    }

    [HttpPost("bankid/collect")]
    public async Task<IActionResult> Collect([FromBody] BankIdCollectRequest request)
    {
        var result = await _authService.CollectBankIdAsync(request, Response);

        if (!result.IsSuccess)
        {
            return Unauthorized(new { message = result.ErrorMessage });
        }

        return Ok(result.CollectData);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerRequestDto request)
    {
        var result = await _authService.RegisterCustomerAsync(request);

        if (!result.IsSuccess)
        {
            if (result.Errors != null)
            {
                return BadRequest(new { message = result.ErrorMessage, errors = result.Errors });
            }

            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(new { token = result.Token });
    }
}