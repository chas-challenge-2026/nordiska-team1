using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Nordiska.BuildingBlocks.Database.Errors;

namespace Nordiska.FrontendApi.Middleware;

/// <summary>
/// Handles exceptions thrown during request processing and converts them to ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _environment;

    //Using dependency injection to get the logger instance for logging errors.
    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService,
        IHostEnvironment environment)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
        _environment = environment;
    }

    /// <summary>
    /// Attempts to handle an exception and write a ProblemDetails response.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Ett ohanterat fel inträffade under bearbetning av {Path}", httpContext.Request.Path);

        // AppException carries its own status/title, so a new exception type never needs a new case here.
        (int statusCode, string title, string detail) = exception switch
        {
            AppException appEx => (appEx.StatusCode, appEx.Title, appEx.Message),
            ArgumentException argEx => (StatusCodes.Status400BadRequest, "Bad Request", argEx.Message),
            UnauthorizedAccessException authEx => (StatusCodes.Status401Unauthorized, "Unauthorized", authEx.Message),

            // An upstream call (e.g. BankID, PDF generation) rejected our credentials.
            HttpRequestException httpEx when httpEx.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                ((int)httpEx.StatusCode.Value, "Upstream Authentication Failed", "Den externa tjänsten avvisade autentiseringen."),

            // An upstream call is rate-limiting us.
            HttpRequestException httpEx when httpEx.StatusCode == (HttpStatusCode)429 =>
                (StatusCodes.Status429TooManyRequests, "Too Many Requests", "För många anrop skickas till en extern tjänst. Försök igen senare."),

            // The dependency itself is fine but doesn't have what we asked for - still our problem, not the caller's,
            // so this stays a 502 (distinct title only) rather than becoming our own 404.
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.NotFound =>
                (StatusCodes.Status502BadGateway, "Upstream Resource Not Found", "Den externa tjänsten kunde inte hitta den efterfrågade resursen."),

            // Any other upstream failure (5xx, or no status code at all e.g. connection refused).
            HttpRequestException => (StatusCodes.Status502BadGateway, "Bad Gateway", "Den externa tjänsten svarade inte som förväntat."),

            // An upstream call took too long to respond.
            TaskCanceledException or TimeoutException =>
                (StatusCodes.Status504GatewayTimeout, "Gateway Timeout", "Anropet till en extern tjänst tog för lång tid."),

            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", DefaultDetail(exception))
        };

        httpContext.Response.StatusCode = statusCode;

        // Routed through IProblemDetailsService (not written by hand) so the app-wide
        // CustomizeProblemDetails hook in ServiceCollectionExtensions still adds traceId.
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
        });
    }

    // Only leak the real exception message in dev; production gets the generic Swedish message.
    private string DefaultDetail(Exception exception) =>
        _environment.IsDevelopment() ? exception.Message : "Ett oväntat fel uppstod.";
}
