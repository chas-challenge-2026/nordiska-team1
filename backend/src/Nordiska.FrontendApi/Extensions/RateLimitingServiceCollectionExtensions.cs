using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Nordiska.FrontendApi.RateLimiting;

namespace Nordiska.FrontendApi.Extensions;

/// <summary>
/// Registers the rate limiting policies for login and money-moving endpoints (NOR-70).
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddRateLimitingPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            // Auth endpoints run before the user is known, so we can only partition per IP.
            options.AddPolicy(RateLimitPolicies.Auth, context =>
            {
                var settings = GetOptions(context).Auth;
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = 0
                });
            });

            // Transactions require [Authorize], so we partition on the customer id from the JWT.
            // (Never on something the client sends, like an account id header, that is easy to get around.)
            options.AddPolicy(RateLimitPolicies.Transactions, context =>
            {
                var settings = GetOptions(context).Transactions;
                var customerId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                                 ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                 ?? "anonymous";

                return RateLimitPartition.GetSlidingWindowLimiter(customerId, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    SegmentsPerWindow = settings.SegmentsPerWindow,
                    QueueLimit = 0
                });
            });

            options.OnRejected = WriteRejectedResponseAsync;
        });

        return services;
    }

    private static RateLimitingOptions GetOptions(HttpContext context)
        => context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    // Returns 429 as ProblemDetails (same shape as GlobalExceptionHandler) with a Retry-After header.
    private static async ValueTask WriteRejectedResponseAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var retryAfter = GetRetryAfter(context);

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        // Log who got limited so attacks are visible. Only IP and customer id, never email or password.
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Nordiska.RateLimiting");
        logger.LogWarning(
            "Rate limit exceeded for {Path}. Ip: {Ip}, CustomerId: {CustomerId}",
            httpContext.Request.Path,
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "anonymous");

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = $"För många försök. Försök igen om {Math.Ceiling(retryAfter.TotalSeconds)} sekunder.",
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json", cancellationToken);
    }

    private static TimeSpan GetRetryAfter(OnRejectedContext context)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            return retryAfter;

        // Sliding window does not always give RetryAfter, fall back to one segment of the transactions window
        var settings = GetOptions(context.HttpContext).Transactions;
        return TimeSpan.FromSeconds((double)settings.WindowSeconds / settings.SegmentsPerWindow);
    }
}
