namespace Nordiska.BuildingBlocks.Database.Errors;

/// <summary>
/// Base for exceptions that GlobalExceptionHandler maps to a specific HTTP status
/// without needing a new switch case for every type.
/// </summary>
public abstract class AppException : Exception
{
    public int StatusCode { get; }

    public string Title { get; }

    protected AppException(int statusCode, string title, string message) : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }
}
