using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace PySpeechService.Client;

/// <summary>
/// Class for PySpeechService service collection extensions
/// </summary>
[SupportedOSPlatform("linux")]
public static class PySpeechServiceExtensions
{
    /// <summary>
    /// Sets up the required PySpeechService services for dependency injection
    /// </summary>
    /// <param name="services">The service collection to add the service to</param>
    /// <returns>The provided service collection</returns>
    public static IServiceCollection AddPySpeechService(this IServiceCollection services)
    {
        services.AddSingleton<IPySpeechServiceFactory, PySpeechServiceFactory>();
        services.AddSingleton<IPySpeechService>(provider => provider.GetRequiredService<IPySpeechServiceFactory>().GetService());
        return services;
    }
}
