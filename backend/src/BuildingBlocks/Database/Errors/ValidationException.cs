namespace Nordiska.BuildingBlocks.Database.Errors;

public sealed class ValidationException : AppException
{
    public ValidationException(string message)
        : base(400, "Valideringsfel", message)
    {
    }
}