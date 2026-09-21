namespace Nordiska.BuildingBlocks.Database.Errors;

public sealed class ConflictException : AppException
{
    // 409 hardcoded here (not StatusCodes.Status409Conflict) because this project doesn't reference ASP.NET Core.
    public ConflictException(string message)
        : base(409, "Otillåten operation", message)
    {
    }
}
