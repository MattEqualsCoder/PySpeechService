// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PySpeechService.Client")]
namespace PySpeechService.TextToSpeech;

/// <summary>
/// Class that is returned when the PySpeechService TTS starts or stops
/// saying a line
/// </summary>
public class SpeakCommandResponse
{
    /// <summary>
    /// The original full request sent to TTS
    /// </summary>
    public string FullMessage { get; internal init; } = "";
    
    /// <summary>
    /// The current line being stated by TTS
    /// </summary>
    public string CurrentChunk { get; internal init; } = "";
    
    /// <summary>
    /// If this is the start of first line of the full request
    /// </summary>
    public bool IsStartOfMessage { get; internal init; }
    
    /// <summary>
    /// If this is the start of a spoken line
    /// </summary>
    public bool IsStartOfChunk { get; internal init; }
    
    /// <summary>
    /// If this is the end of the last line of a full request
    /// </summary>
    public bool IsEndOfMessage { get; internal init; }
    
    /// <summary>
    /// If this is the end of a spoken line
    /// </summary>
    public bool IsEndOfChunk { get; internal init; }
    
    /// <summary>
    /// If there is another queued message after this one
    /// </summary>
    public bool HasAnotherRequest { get; internal init; }
    
    /// <summary>
    /// The message id to identify the message
    /// </summary>
    public string? MessageId { get; internal init; }
}

/// <summary>
/// EventArgs for receiving an update from PySpeechService's TTS
/// </summary>
/// <param name="response"></param>
public class SpeakCommandResponseEventArgs(SpeakCommandResponse response) : EventArgs
{
    /// <summary>
    /// Response objects with the current details from TTS
    /// </summary>
    public SpeakCommandResponse Response { get; } = response;
}