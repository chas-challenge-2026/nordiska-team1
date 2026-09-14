using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication;
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
        var result = await _authService.RegisterCustomerAsync(request, Response);

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

    /// <summary>
    /// Returns the currently authenticated user's profile information extracted from the JWT.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        var customerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value 
                         ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value 
                    ?? User.FindFirst(ClaimTypes.Email)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            id = customerId,
            email = email,
            role = role
        });
    }

    /// <summary>
    /// Logs out the user by clearing the HTTP-only authentication cookie.
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.DeleteAuthCookie();
        return Ok(new { message = "Logged out successfully" });
    }
}