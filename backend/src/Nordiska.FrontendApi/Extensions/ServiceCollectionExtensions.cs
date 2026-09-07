namespace Nordiska.FrontendApi.Extensions;

using Nordiska.FrontendApi.Middleware;
//Put service collection extension methods here for better organization and separation of concerns. (TO KEEP Prgram.cs CLEAN)
public static class ServiceCollectionExtensions
{
    // Add error handling services to the service collection
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}