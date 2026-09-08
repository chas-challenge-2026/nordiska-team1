using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Nordiska.FrontendApi.Middleware;

public sealed class AccountIdExtractionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AccountIdExtractionMiddleware> _logger;

    public AccountIdExtractionMiddleware(RequestDelegate next, ILogger<AccountIdExtractionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only consider POST requests to the transactions endpoint
        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.StartsWithSegments("/api/transactions", StringComparison.OrdinalIgnoreCase))
        {
            var contentType = context.Request.ContentType ?? string.Empty;
            if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    context.Request.EnableBuffering();
                    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("Type", out var typeProp) &&
                            string.Equals(typeProp.GetString(), "withdrawal", StringComparison.OrdinalIgnoreCase))
                        {
                            if (root.TryGetProperty("AccountId", out var accProp) && accProp.ValueKind == JsonValueKind.Number)
                            {
                                try
                                {
                                    var accountId = accProp.GetInt64().ToString();
                                    // Set header so the rate limiter can partition per-account
                                    context.Request.Headers["X-Account-Id"] = accountId;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "Failed to parse AccountId from request body for rate limiting partitioning.");
                                }
                            }
                        }
                    }
                }
                catch (JsonException jex)
                {
                    _logger.LogDebug(jex, "Invalid JSON in transaction request body when extracting AccountId for rate limiting.");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Unexpected error while extracting AccountId for rate limiting.");
                }
            }
        }

        await _next(context);
    }
}
