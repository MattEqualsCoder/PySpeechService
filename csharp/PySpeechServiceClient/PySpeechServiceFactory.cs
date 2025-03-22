using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PySpeechServiceClient;

internal class PySpeechServiceFactory(IServiceProvider serviceProvider) : IPySpeechServiceFactory
{
    private IPySpeechService? _instance;
    
    public IPySpeechService GetService()
    {
        if (_instance != null)
        {
            return _instance;
        }
        
        var logger = serviceProvider.GetService<ILogger<IPySpeechService>>();
        var builder = new PySpeechServiceBuilder();
        if (logger != null)
        {
            builder.AddLogger(logger);
        }
        return _instance = builder.Build();
    }
}