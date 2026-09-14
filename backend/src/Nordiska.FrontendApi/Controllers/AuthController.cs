using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.Contracts.Responses;
using Nordiska.Modules.Banking.Application;

namespace Nordiska.FrontendApi.Controllers;

/// <summary>
/// Authentication and session management endpoints using BankID and JWT cookies.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Initiates a BankID authentication session.
    /// </summary>
    /// <remarks>
    /// Starts a BankID transaction. In the development simulator environment, an optional 
    /// <c>personalNum</c> can be supplied to simulate authenticating as a specific customer.
    /// Returns an <c>orderRef</c> that must be passed to the <c>/api/auth/bankid/collect</c> endpoint.
    /// </remarks>
    /// <param name="request">The initiate request containing the optional personal number.</param>
    /// <response code="200">BankID session successfully initiated with orderRef and start tokens.</response>
    /// <response code="400">Failed to initiate BankID session (e.g. invalid request or service error).</response>
    [HttpPost("bankid/initiate")]
    [ProducesResponseType(typeof(BankIdInitiateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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

    /// <summary>
    /// Collects the status of an ongoing BankID authentication session.
    /// </summary>
    /// <remarks>
    /// Polls the BankID session using the <c>orderRef</c>. When the status reaches <c>COMPLETE</c>,
    /// a secure, HttpOnly authentication cookie (<c>access_token</c>) is automatically set on the response.
    /// </remarks>
    /// <param name="request">The collect request containing the orderRef.</param>
    /// <response code="200">Current status of the authentication (e.g. PENDING or COMPLETE with customer profile).</response>
    /// <response code="401">Authentication failed or customer not found.</response>
    [HttpPost("bankid/collect")]
    [ProducesResponseType(typeof(BankIdCollectResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Collect([FromBody] BankIdCollectRequest request)
    {
        var result = await _authService.CollectBankIdAsync(request, Response);

        if (!result.IsSuccess)
        {
            return Unauthorized(new { message = result.ErrorMessage });
        }

        return Ok(result.CollectData);
    }

    /// <summary>
    /// Registers a new customer and establishes an authenticated session.
    /// </summary>
    /// <remarks>
    /// Creates a new customer identity record and immediately sets the HttpOnly authentication cookie.
    /// </remarks>
    /// <param name="request">Customer registration details including name, email, personal number, and phone number.</param>
    /// <response code="200">Customer registered successfully and session established.</response>
    /// <response code="400">Registration validation failed or email already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
    /// <response code="200">The authenticated user's id, email, and role.</response>
    /// <response code="401">Unauthorized if the user is not authenticated.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    /// <response code="200">Successfully logged out and cookie deleted.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        Response.DeleteAuthCookie();
        return Ok(new { message = "Logged out successfully" });
    }
}