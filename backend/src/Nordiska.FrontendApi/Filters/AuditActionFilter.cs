using System.Security.Claims;
using System.Text.Json;
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
            var nameIdentifier = executedContext.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(nameIdentifier, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var ipAddress = executedContext.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var requestPath = executedContext.HttpContext.Request.Path.Value ?? string.Empty;
            var httpMethod = executedContext.HttpContext.Request.Method;

            // Sanitize and serialize action arguments (avoid logging passwords/sensitive secrets/PII if present)
            var sanitizedArgs = new Dictionary<string, object?>();
            foreach (var (key, value) in context.ActionArguments)
            {
                if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("pin", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("ssn", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("personnummer", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("cvv", StringComparison.OrdinalIgnoreCase))
                {
                    sanitizedArgs[key] = "[REDACTED]";
                }
                else
                {
                    sanitizedArgs[key] = value;
                }
            }

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
}
