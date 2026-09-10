using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nordiska.FrontendApi.Authentication;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.Modules.Faq.Infrastructure.Db;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Nordiska.Modules.Reporting.Infrastructure.Db;
using Nordiska.Modules.Faq.Application;
using System.IO;
using System.Reflection;
using Scalar.AspNetCore;
using Nordiska.FrontendApi.Extensions;
using Microsoft.AspNetCore.Identity;
using Nordiska.Modules.Banking.Domain;
using ActiveLogin.Authentication.BankId.AspNetCore.Auth;
using ActiveLogin.Authentication.BankId.Api;
using ActiveLogin.Authentication.BankId.Core;
using Microsoft.EntityFrameworkCore;
using Nordiska.Modules.Banking.Infrastructure;

using Microsoft.OpenApi;
using Nordiska.Modules.Banking.Application;

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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthCookieExtensions.CookieName, out var cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("faq:manage", policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme);

        policy.RequireAuthenticatedUser();

        policy.RequireClaim(
            "permission",
            "faq:manage");
    });
});
// Register JWT Provider in Dependency Injection
builder.Services.AddScoped<IJwtProvider, JwtProvider>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Register controller services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // include XML comments so Scalar/Swagger can show summaries and parameter docs
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Configure JWT Bearer authentication in Swagger / Scalar UI
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Klistra in ditt JWT-token här (utan 'Bearer ' prefix)."
    };
    options.AddSecurityDefinition("Bearer", securityScheme);

    options.AddSecurityRequirement((doc) => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});

builder.Services.AddFaqModuleInfrastructure(builder.Configuration);

builder.Services.AddReportingModuleInfrastructure(builder.Configuration);

builder.Services.AddBankingModuleInfrastructure(builder.Configuration);
 
builder.Services
    .AddIdentityCore<Customer>(options =>
    {
        // Customer log in with bank ID, we do not need to store password in database when creating new customer. 
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 1;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequiredUniqueChars = 0;
        
        // Email must be unique
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<long>>()
    .AddEntityFrameworkStores<BankingDbContext>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            System.Diagnostics.Activity.Current?.Id
            ?? context.HttpContext.TraceIdentifier;
    };
});


// Get environment from app settings 
var bankIdEnvironment = builder.Configuration["ActiveLogin:BankId:Environment"] ?? "Simulated";
// Service for bank id  
if (bankIdEnvironment.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddBankId(bankId => bankId.UseSimulatedEnvironment());
}
else if (bankIdEnvironment.Equals("Test", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddBankId(bankId => bankId.UseTestEnvironment());
}
builder.Services
    .AddAuthentication()
    .AddBankIdAuth(bankId =>
    {
        bankId.AddSameDevice();
    });
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

if (app.Environment.IsDevelopment())
{
    app.MapSwagger("/openapi/{documentName}.json");

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Nordiska API");

        // These optional features aren't needed for local API testing.
        options.DisableAgent();
        options.DisableDefaultFonts();

        // Show C# HttpClient examples by default.
        options.WithDefaultHttpClient(
            ScalarTarget.CSharp,
            ScalarClient.HttpClient);
    });
    app.MapGet("/health/database", async (
        BankingDbContext db,
        CancellationToken cancellationToken) =>
    {
        var connected = await db.Database.CanConnectAsync(
            cancellationToken);

        return connected
            ? Results.Ok(new { status = "connected" })
            : Results.Json(
                new { status = "unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
    });
}

// Seed Test Customer 
if (app.Environment.IsDevelopment())
{
    await DbInitializer.SeedAsync(app.Services);
}


app.Run();

// Expose Program class for integration testing with WebApplicationFactory
public partial class Program { }

