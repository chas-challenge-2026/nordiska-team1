using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nordiska.FrontendApi.Authentication.Jwt;
using Nordiska.Modules.Banking.Infrastructure;
using Nordiska.Modules.Banking.Infrastructure.DbConfigs;
using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Infrastructure.DbConfigs;
using Nordiska.Modules.Reporting.Infrastructure;
using Nordiska.Modules.Reporting.Infrastructure.DbConfigs;


var builder = WebApplication.CreateBuilder(args);




builder.Services.AddBankingModuleInfrastructure(
    builder.Configuration);

builder.Services.AddReportingModuleInfrastructure(
    builder.Configuration);

builder.Services.AddFaqModuleInfrastructure(
    builder.Configuration);




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

var app = builder.Build();

app.UseHttpsRedirection();

// Enable authentication and authorization middleware in the pipeline
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

 if (app.Environment.IsDevelopment())
{

      app.MapGet("/config-test", (IConfiguration configuration) =>
    {
        return Results.Ok(new
        {
            BankingDatabase =
                !string.IsNullOrWhiteSpace(
                    configuration.GetConnectionString("BankingDatabase")),

            ReportingDatabase =
                !string.IsNullOrWhiteSpace(
                    configuration.GetConnectionString("ReportingDatabase")),

            FaqDatabase =
                !string.IsNullOrWhiteSpace(
                    configuration.GetConnectionString("FaqDatabase"))
        });
    });

    app.MapGet("/db-test", async (
        BankingDbContext banking,
        ReportDbContext reporting,
        FaqDbContext faq) =>
    {
        return Results.Ok(new
        {
            Banking = await banking.Database.CanConnectAsync(),
            Reporting = await reporting.Database.CanConnectAsync(),
            Faq = await faq.Database.CanConnectAsync()
        });
    });
}

 

app.Run();

 