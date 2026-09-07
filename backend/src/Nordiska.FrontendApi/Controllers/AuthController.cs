using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.Modules.Banking.Domain;

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

            return Ok(new LoginResponse(token));
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

            return Ok(new LoginResponse(token));
        }

        return Unauthorized(new
        {
            Message = "Invalid email or password."
        });
    }
}

public record LoginRequest(
    string Email,
    string Password);

public record LoginResponse(
    string Token);