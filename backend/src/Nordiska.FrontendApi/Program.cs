using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.Modules.Faq.Infrastructure.Db;
using Nordiska.Modules.Banking.Infrastructure.Db;
using Nordiska.Modules.Reporting.Infrastructure.Db;
using Nordiska.Modules.Faq.Application;
using Scalar.AspNetCore;

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

// Register controller services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddFaqModuleInfrastructure(builder.Configuration);

builder.Services.AddReportingModuleInfrastructure(builder.Configuration);

builder.Services.AddBankingModuleInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            System.Diagnostics.Activity.Current?.Id
            ?? context.HttpContext.TraceIdentifier;
    };
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseHttpsRedirection();

// Enable authentication and authorization middleware in the pipeline
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
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

app.Run();


