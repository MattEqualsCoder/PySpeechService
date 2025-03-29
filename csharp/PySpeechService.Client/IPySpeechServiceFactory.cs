using System.Runtime.Versioning;

namespace PySpeechService.Client;

public interface IPySpeechServiceFactory
{
    [SupportedOSPlatform("linux")]
    public IPySpeechService GetService();
}