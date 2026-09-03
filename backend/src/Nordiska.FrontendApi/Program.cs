using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nordiska.FrontendApi.Authentication.Jwt;

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

var app = builder.Build();

app.UseHttpsRedirection();

// Enable CORS middleware before Authentication and Authorization
app.UseCors(StrictFrontendCorsPolicy);

// Enable authentication and authorization middleware in the pipeline
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Keep default weather forecast endpoint
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Expose Program class for integration testing with WebApplicationFactory
public partial class Program { }

