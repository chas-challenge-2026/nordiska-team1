using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nordiska.FrontendApi.Authentication;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.FrontendApi.Controllers;

/// <summary>
/// Authentication endpoints for local/demo login flows.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IJwtProvider _jwtProvider;
    private readonly JwtOptions _jwtOptions;

    public AuthController(IJwtProvider jwtProvider, IOptions<JwtOptions> jwtOptions)
    {
        _jwtProvider = jwtProvider;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Performs a demo login, sets an HTTP-only JWT cookie, and returns the authenticated user info.
    /// </summary>
    /// <param name="request">Login request containing email and password.</param>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request.Email == "anna@example.com" &&
            request.Password == "password123")
        {
            var customer = new Customer
            {
                Id = 1,
                Email = "anna@example.com",
                UserName = "anna@example.com",
                Name = "Anna"
            };

            var token = await _jwtProvider.Generate(customer);
            Response.AppendAuthCookie(token, _jwtOptions.TokenLifetimeInMinutes);

            return Ok(new
            {
                message = "Login successful",
                customer = new { id = customer.Id, email = customer.Email, name = customer.Name }
            });
        }

        if (request.Email == "erik@example.com" &&
            request.Password == "password123")
        {
            var customer = new Customer
            {
                Id = 2,
                Email = "erik@example.com",
                UserName = "erik@example.com",
                Name = "Erik"
            };

            var token = await _jwtProvider.Generate(customer);
            Response.AppendAuthCookie(token, _jwtOptions.TokenLifetimeInMinutes);

            return Ok(new
            {
                message = "Login successful",
                customer = new { id = customer.Id, email = customer.Email, name = customer.Name }
            });
        }

        return Unauthorized(new
        {
            Message = "Invalid email or password."
        });
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

public record LoginRequest(
    string Email,
    string Password);
