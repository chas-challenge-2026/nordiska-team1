using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.FrontendApi.Extensions;

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

// Configure strict CORS policy for the React 18 SPA (NOR-66)
// Whitelists trusted frontend origins without AllowAnyOrigin.
// Enables Authorization header for JWT tokens and exposes Content-Disposition for PDF downloads.
const string StrictFrontendCorsPolicy = "StrictFrontendCorsPolicy";

builder.Services.AddCors(options =>
{
    options.AddPolicy(StrictFrontendCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[]
            {
                "http://localhost:5173",
                "http://localhost:5174",
                "http://localhost:4173",
                "http://localhost:3000"
            };

        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "PATCH", "OPTIONS")
              .WithHeaders("Authorization", "Content-Type", "Accept", "X-Requested-With")
              .WithExposedHeaders("Content-Disposition")
              .AllowCredentials();
    });
});
// Custom-made! ProblemDetails and ExceptionHandler DI registered via extension (moved into ServiceCollectionExtensions.cs)
builder.Services.AddErrorHandling();

var app = builder.Build();

//look out for the order of middleware, it matters.
app.UseExceptionHandler();
app.UseHttpsRedirection();

// Enable authentication and authorization middleware in the pipeline
app.UseRouting();
// Enable CORS middleware before Authentication and Authorization
app.UseCors(StrictFrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Expose Program class for integration testing with WebApplicationFactory
public partial class Program { }

