using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nordiska.Modules.Banking.Domain;

namespace Nordiska.FrontendApi.Authentication.Jwt;

/// <summary>
/// Produces JSON Web Tokens for authenticated customers.
/// </summary>
public class JwtProvider : IJwtProvider
{
    private readonly JwtOptions _options;
    private readonly UserManager<Customer> _userManager;

    public JwtProvider(IOptions<JwtOptions> options,  UserManager<Customer> userManager)
    {
        _options = options.Value;
        _userManager = userManager;
    }
    

    /// <summary>
    /// Generates a signed JWT for the provided customer.
    /// </summary>
    /// <param name="customer">Customer to generate token for.</param>
    /// <returns>JWT as a string.</returns>
    public async Task<string> Generate(Customer customer)
    {
        
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, customer.Email ?? string.Empty),
        };
        
        try
        {
            var roles = await _userManager.GetRolesAsync(customer);
            if (roles.Count > 0)
            {
                claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, "Customer"));
            }
        }
        catch
        {
            // Fallback when Identity tables are not mapped in DbContext or customer is in-memory
            claims.Add(new Claim(ClaimTypes.Role, "Customer"));
        }

        var key = Encoding.UTF8.GetBytes(_options.SecretKey);
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_options.TokenLifetimeInMinutes),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
