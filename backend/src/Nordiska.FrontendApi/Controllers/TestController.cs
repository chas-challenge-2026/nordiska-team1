using System.Linq;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nordiska.FrontendApi.Controllers;

[ApiController]
[Route("api/test")]
[Authorize]
public class TestController : ControllerBase
{
    [HttpGet("secure")]
    public IActionResult GetSecureData()
    {
        // Fetch all claims from the user's token
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        
        // Fetch specific claims
        var customerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value 
                         ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                         
        var email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value 
                    ?? User.FindFirst(ClaimTypes.Email)?.Value;
                    
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            Message = "You have successfully accessed a protected endpoint with a valid JWT!",
            CustomerId = customerId,
            Email = email,
            Role = role,
            AllClaims = claims
        });
    }
}
