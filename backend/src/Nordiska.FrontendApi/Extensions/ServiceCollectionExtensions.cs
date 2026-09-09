namespace Nordiska.FrontendApi.Extensions;

using Nordiska.FrontendApi.Middleware;
//Put service collection extension methods here for better organization and separation of concerns. (TO KEEP Prgram.cs CLEAN)
/// <summary>
/// Convenience extension methods for IServiceCollection used by the Frontend API.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers problem details and the global exception handler.
    /// </summary>
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
