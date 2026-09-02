using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication.Jwt;

namespace Nordiska.FrontendApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IJwtProvider _jwtProvider;

    public AuthController(IJwtProvider jwtProvider)
    {
        _jwtProvider = jwtProvider;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Mock users matching test credentials in README.md
        if (request.Email == "anna@example.com" && request.Password == "password123")
        {
            // Generate token with ID 1 (bigint) and Customer role
            var token = _jwtProvider.Generate(1, "anna@example.com", "Customer");
            return Ok(new LoginResponse(token));
        }

        if (request.Email == "erik@example.com" && request.Password == "password123")
        {
            // Generate token with ID 2 (bigint) and Customer role
            var token = _jwtProvider.Generate(2, "erik@example.com", "Customer");
            return Ok(new LoginResponse(token));
        }

        return Unauthorized(new { Message = "Invalid email or password." });
    }
}

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token);
