using System.Linq;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nordiska.FrontendApi.Controllers;

/// <summary>
/// Controller providing test endpoints for authenticated scenarios.
/// </summary>
[ApiController]
[Route("api/test")]
[Authorize]
public class TestController : ControllerBase
{
    /// <summary>
    /// Returns information extracted from the authenticated user's JWT claims.
    /// </summary>
    /// <returns>Basic user info and all claims.</returns>
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
