using PySpeechServiceClient.Grammar;
using PySpeechServiceClient.Models;
// ReSharper disable UnusedMember.Global
// ReSharper disable EventNeverSubscribedTo.Global

namespace PySpeechServiceClient;

/// <summary>
/// Service for calling the PySpeechService application
/// </summary>
public interface IPySpeechService : IDisposable
{
    /// <summary>
    /// If PySpeechService is running and connected
    /// </summary>
    public bool IsConnected { get; }
 
    /// <summary>
    /// If TTS is current enabled
    /// </summary>
    public bool IsSpeechEnabled { get; }
 
    /// <summary>
    /// If speech recognition is enabled
    /// </summary>
    public bool IsSpeechRecognitionEnabled { get; }
    
    /// <summary>
    /// If the PySpeechService should be restarted and connected to if connection is lost
    /// </summary>
    public bool AutoReconnect { get; set; }
    
    /// <summary>
    /// Event for when the PySpeechService has finished launching and has
    /// been successfully connected to
    /// </summary>
    public event EventHandler Initialized;

    /// <summary>
    /// Event for when the connection to PySpeechService has been lost
    /// </summary>
    public event EventHandler Disconnected;
    
    /// <summary>
    /// Event for when text to speech has finished initializing, including
    /// after downloading the piper text to speech files, if needed
    /// </summary>
    public event EventHandler TextToSpeechInitialized;
    
    /// <summary>
    /// Event for when speech recognition has finised initializing, including
    /// after downloading the vosk files
    /// </summary>
    public event EventHandler SpeechRecognitionInitialized;
    
    /// <summary>
    /// Event for when the PySpeechService's TTS has either started or stopped
    /// speaking a line
    /// </summary>
    public event EventHandler<SpeakCommandResponseEventArgs> SpeakCommandResponded;

    /// <summary>
    /// Event for when speech has been recognized from PySpeechService
    /// </summary>
    public event EventHandler<SpeechRecognitionResultEventArgs> SpeechRecognized;
     
    /// <summary>
    /// Start and connect to the PySpeechService application
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    public Task<bool> StartAsync();

    /// <summary>
    /// Add a command to listen
    /// </summary>
    /// <param name="command">The grammar with details about what to listen for</param>
    public void AddSpeechRecognitionCommand(SpeechRecognitionGrammar command);

    /// <summary>
    /// Add replacements for words that the VOSK speech recognition does not normally pick up
    /// </summary>
    /// <param name="replacements">A dictionary of the phrase for VOSK to listen for as the key
    /// and the text to replace it with when determining what the user said as the value.</param>
    public void AddSpeechRecognitionReplacements(IDictionary<string, string> replacements);

    /// <summary>
    /// Request the PySpeechService to speak a line
    /// </summary>
    /// <param name="message">The message to speak</param>
    /// <param name="details">Optional voice settings the override the defaults with</param>
    public void Speak(string message, Models.SpeechSettings? details = null);
    
    /// <summary>
    /// Request the PySpeechService to speak a line
    /// </summary>
    /// <param name="message">The message to speak</param>
    /// <param name="details">Optional voice settings the override the defaults with</param>
    /// <returns>True if the message was successfully queued.</returns>
    public Task<bool> SpeakAsync(string message, Models.SpeechSettings? details = null);
    
    /// <summary>
    /// Stop the PySpeechService speaking and clear all queued lines
    /// </summary>
    /// <returns>True if the message was successfully queued.</returns>
    public Task<bool> StopSpeakingAsync();

    /// <summary>
    /// Sets the default voice settings for when TTS is speaking
    /// </summary>
    /// <returns>True if the message was successfully queued.</returns>
    public Task<bool> SetSpeechSettingsAsync(Models.SpeechSettings settings);

    /// <summary>
    /// Starts speech recognition and passes the entered commands to the PySpeechService
    /// application to listen for
    /// </summary>
    /// <param name="voskModel">The VOSK voice recognition model to use. en-us small is used if not procided.</param>
    /// <param name="requiredConfidence">The required confidence for matching the text spoken</param>
    /// <param name="prefix">Prefix for all speech recognition statements</param>
    /// <returns>True if the request was successfully sent to the PySpeechService application</returns>
    public Task<bool> StartSpeechRecognitionAsync(string? voskModel = null, double requiredConfidence = 80, string prefix = "");
    
    /// <summary>
    /// Stops the PySpeechService application from listening for speech
    /// </summary>
    /// <returns>True if the request was successfully sent to the PySpeechService application</returns>
    public Task<bool> StopSpeechRecognitionAsync();

    /// <summary>
    /// Tells the PySpeechService application to terminate
    /// </summary>
    /// <returns>True if the request was successfully sent to the PySpeechService application</returns>
    public Task<bool> ShutdownAsync();

    /// <summary>
    /// Sets the default volume for text to speech
    /// </summary>
    /// <param name="volume">The volume ratio with 0 being silent, 1 being default, and 2 being twice as loud</param>
    /// <returns>True if the request was successfully sent to the PySpeechService application</returns>
    public Task<bool> SetVolumeAsync(double volume);
}