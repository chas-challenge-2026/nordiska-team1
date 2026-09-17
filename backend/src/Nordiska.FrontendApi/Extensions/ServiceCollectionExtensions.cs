namespace Nordiska.FrontendApi.Extensions;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Nordiska.FrontendApi.Middleware;
//Put service collection extension methods here for better organization and separation of concerns. (TO KEEP Prgram.cs CLEAN)
/// <summary>
/// Convenience extension methods for IServiceCollection used by the Frontend API.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers problem details and the global exception handler.
    /// </summary>
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Registers authorization policies. Every endpoint requires a logged in user unless it is marked with [AllowAnonymous].
    /// </summary>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Safe by default: a new endpoint without attributes is protected, not public
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

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

        return services;
    }
}
