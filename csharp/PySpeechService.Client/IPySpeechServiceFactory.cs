using System.Runtime.Versioning;

namespace PySpeechService.Client;

[SupportedOSPlatform("linux")]
public interface IPySpeechServiceFactory
{
    public IPySpeechService GetService();
}