namespace Nordiska.BuildingBlocks.Database.Errors;

public sealed class NotFoundException : AppException
{
    // 404 hardcoded here (not StatusCodes.Status404NotFound) because this project doesn't reference ASP.NET Core.
    public NotFoundException(string message)
        : base(404, "Resurs hittades inte", message)
    {
    }
}
