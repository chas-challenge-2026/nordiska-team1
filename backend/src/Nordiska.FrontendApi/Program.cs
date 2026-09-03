using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.FrontendApi.Extensions;

using ActiveLogin.Authentication.BankId.Api;
using ActiveLogin.Authentication.BankId.Core;

var builder = WebApplication.CreateBuilder(args);

// Register JWT configuration options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// Configure authentication with JWT Bearer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is missing.")))
        };
    });

// Configure authorization policies (if needed) this came from the default template, but you can customize it as needed
builder.Services.AddAuthorization();

// Register JWT Provider in Dependency Injection
builder.Services.AddScoped<IJwtProvider, JwtProvider>();

// Register controller services
builder.Services.AddControllers();


// Get environment from app settings 
var bankIdEnvironment = builder.Configuration["ActiveLogin:BankId:Environment"] ?? "Simulated";
// Service for bank id  
builder.Services.AddBankId(bankId =>
{
    
    if (bankIdEnvironment.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
    {
        bankId.UseSimulatedEnvironment();
    }
    else if (bankIdEnvironment.Equals("Test", StringComparison.OrdinalIgnoreCase))
    {
        bankId.UseTestEnvironment();
        // Add real certificate, ex from azure key vault below. 
    }
});
// Custom-made! ProblemDetails and ExceptionHandler DI registered via extension (moved into ServiceCollectionExtensions.cs)
builder.Services.AddErrorHandling();

var app = builder.Build();

//look out for the order of middleware, it matters.
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
