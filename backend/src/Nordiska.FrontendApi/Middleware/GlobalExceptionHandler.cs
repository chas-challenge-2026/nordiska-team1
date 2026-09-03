using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Nordiska.FrontendApi.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    //Using dependency injection to get the logger instance for logging errors.
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    // This method handles exceptions and returns appropriate HTTP responses based on the exception type.
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Ett ohanterat fel inträffade: {Message}", exception.Message);

        // Map specific exceptions to HTTP status codes and messages
        (int statusCode, string title, string detail) = exception switch
        {
            ArgumentException argEx => (StatusCodes.Status400BadRequest, "Bad Request", argEx.Message),
            KeyNotFoundException notFoundEx => (StatusCodes.Status404NotFound, "Not Found", notFoundEx.Message),
            UnauthorizedAccessException authEx => (StatusCodes.Status401Unauthorized, "Unauthorized", authEx.Message),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "Ett oväntat fel uppstod.")
        };

        // Turns the response into a ProblemDetails object, more readable and JSON-friendly (esp frontend clients in oour case).
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        // Adding a traceId to the response for easier debugging and correlation of logs (for our logger later).
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        // Set the response status code and write the ProblemDetails object as JSON to the response body.
        httpContext.Response.StatusCode = statusCode;
        // Set the content type to application/json for the response.
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        //Returns happy path, indicating that the exception was handled successfully. Good for the middleware pipeline to know that it can stop further processing.
        return true;
    }
}