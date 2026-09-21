using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nordiska.Modules.Reporting.Application;

namespace Nordiska.FrontendApi.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AuditActionAttribute : TypeFilterAttribute
{
    public AuditActionAttribute(string action) : base(typeof(AuditActionFilter))
    {
        Arguments = new object[] { action };
    }
}

public sealed class AuditActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "secret",
        "token",
        "pin",
        "ssn",
        "personnummer",
        "personalnum",
        "personalnumber",
        "cvv",
        "cvc",
        "key",
        "authorization",
        "pwd"
    };

    private readonly string _action;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditActionFilter> _logger;

    public AuditActionFilter(
        string action,
        IAuditLogService auditLogService,
        ILogger<AuditActionFilter> logger)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Execute the action first
        var executedContext = await next();

        // Only audit if action completed without unhandled exception or failed HTTP response
        if (executedContext.Exception != null && !executedContext.ExceptionHandled)
        {
            return;
        }

        var statusCode = executedContext.Result switch
        {
            ObjectResult obj => obj.StatusCode ?? 200,
            StatusCodeResult status => status.StatusCode,
            _ => 200
        };

        if (statusCode >= 400)
        {
            return;
        }

        try
        {
            long? userId = null;
            var nameIdentifier = executedContext.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                 ?? executedContext.HttpContext.User.FindFirst("sub")?.Value;
            if (long.TryParse(nameIdentifier, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var ipAddress = executedContext.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var requestPath = executedContext.HttpContext.Request.Path.Value ?? string.Empty;
            var httpMethod = executedContext.HttpContext.Request.Method;

            // Deep sanitize and serialize action arguments (recursively masks passwords, secrets, tokens, PII)
            var sanitizedArgs = SanitizeArguments(context.ActionArguments);

            var detailsObject = new
            {
                path = requestPath,
                method = httpMethod,
                ip = ipAddress,
                statusCode,
                arguments = sanitizedArgs
            };

            var detailsJson = JsonSerializer.Serialize(detailsObject);

            await _auditLogService.LogAsync(_action, userId, detailsJson, executedContext.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            // Log audit failure but do not break user response
            _logger.LogError(ex, "Failed to write audit entry for action {Action}", _action);
        }
    }

    public static JsonNode? SanitizeArguments(IDictionary<string, object?> actionArguments)
    {
        try
        {
            var node = JsonSerializer.SerializeToNode(actionArguments);
            return SanitizeNode(node);
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static JsonNode? SanitizeNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var sanitized = new JsonObject();
            foreach (var kvp in obj)
            {
                if (IsSensitive(kvp.Key))
                {
                    sanitized[kvp.Key] = "[REDACTED]";
                }
                else
                {
                    sanitized[kvp.Key] = SanitizeNode(kvp.Value?.DeepClone());
                }
            }
            return sanitized;
        }

        if (node is JsonArray arr)
        {
            var sanitized = new JsonArray();
            foreach (var item in arr)
            {
                sanitized.Add(SanitizeNode(item?.DeepClone()));
            }
            return sanitized;
        }

        return node?.DeepClone();
    }

    private static bool IsSensitive(string propertyName)
    {
        return SensitiveKeys.Any(k => propertyName.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
