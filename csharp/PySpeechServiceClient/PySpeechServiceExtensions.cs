using Microsoft.Extensions.DependencyInjection;

namespace PySpeechServiceClient;

public static class PySpeechServiceExtensions
{
    public static IServiceCollection AddPySpeechService(this IServiceCollection services)
    {
        services.AddSingleton<IPySpeechService, PySpeechService>();
        services.AddSingleton<PySpeechServiceRunner>();
        return services;
    }
}
