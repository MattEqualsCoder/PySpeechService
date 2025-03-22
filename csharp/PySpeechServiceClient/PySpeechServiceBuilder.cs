using Microsoft.Extensions.Logging;

namespace PySpeechServiceClient;

public class PySpeechServiceBuilder
{
    private ILogger<IPySpeechService>? _logger;

    public PySpeechServiceBuilder AddLogger(ILogger<IPySpeechService> logger)
    {
        _logger = logger;
        return this;
    }

    public IPySpeechService Build()
    {
        var runner = new PySpeechServiceRunner();
        runner.Logger = _logger;

        var service = new PySpeechService(runner);
        service.Logger = _logger;
        
        return service;
    }
}