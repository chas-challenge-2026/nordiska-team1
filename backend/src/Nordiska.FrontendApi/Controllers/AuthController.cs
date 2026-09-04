using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.FrontendApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IJwtProvider _jwtProvider;
    private readonly UserManager<Customer> _userManager;

    public AuthController(IJwtProvider jwtProvider,
        UserManager<Customer> userManager)
    {
        _jwtProvider = jwtProvider;
        _userManager = userManager;
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        var customer = await _userManager.FindByEmailAsync(request.Email);
        if(customer is null) 
            return Unauthorized(new {Message = "Invalid email or password."});
        var validPassword = await _userManager.CheckPasswordAsync(customer, request.Password);
        if(!validPassword) 
            return Unauthorized(new {Message = "Invalid email or password."});

        // Generate JWT using the authenticated customers identity and roles
        var token = await _jwtProvider.Generate(customer);
        return Ok(new LoginResponse(token));
    }
}
public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token);
