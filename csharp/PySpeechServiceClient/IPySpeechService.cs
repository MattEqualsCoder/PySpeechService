using System.Speech.Synthesis;
using PySpeechServiceClient.Grammar;
using PySpeechServiceClient.Models;

namespace PySpeechServiceClient;

public interface IPySpeechService : IDisposable
{
    public bool IsConnected { get; }
 
    public bool IsSpeechEnabled { get; }
 
    public bool IsSpeechRecognitionEnabled { get; }
    
    public event EventHandler Started;

    public event EventHandler Stopped;
    public event EventHandler TextToSpeechInitialized;
    public event EventHandler SpeechRecognitionInitialized;
    
    public event EventHandler<SpeakCommandResponseEventArgs> SpeakCommandResponded;

    public event EventHandler<SpeechRecognitionResultEventArgs> SpeechRecognized;
     
    public Task<bool> StartAsync();

    public void AddSpeechRecognitionCommand(SpeechRecognitionGrammar command);

    public Task<bool> SpeakAsync(string message, Models.SpeechSettings? details = null);
    
    public Task StopSpeakingAsync();

    public Task SetSpeechSettingsAsync(Models.SpeechSettings settings);

    public Task StartSpeechRecognitionAsync(string? voskModel = null, double requiredConfidence = 80);

    public Task ShutdownAsync();
}