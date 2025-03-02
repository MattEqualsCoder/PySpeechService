// ReSharper disable UnusedMember.Global
namespace PySpeechServiceClient.Models;

/// <summary>
/// A key value pair that was matched from what the user stated
/// </summary>
/// <param name="key">The group key</param>
/// <param name="value">The value matching what the user said</param>
public class SpeechRecognitionSemantic(string key, string value)
{
    /// <summary>
    /// The group key
    /// </summary>
    public string Key => key;
    
    /// <summary>
    /// The value matching what the user said
    /// </summary>
    public string Value => value;
}

public class SpeechRecognitionResult : EventArgs
{
    /// <summary>
    /// The text recognized by Speech Recognition
    /// </summary>
    public required string Text { get; set; }
    
    /// <summary>
    /// The confidence in the recognition result
    /// </summary>
    public required float Confidence { get; set; }
    
    /// <summary>
    /// A dictionary options that the user had to pick from and what they picked
    /// </summary>
    public Dictionary<string, SpeechRecognitionSemantic> Semantics { get; set; } = [];
    
    /// <summary>
    /// The native speech recognition result if on Windows and using the built-in
    /// Windows speech recognition
    /// </summary>
    public System.Speech.Recognition.RecognitionResult? NativeResult { get; set; }
}

/// <summary>
/// Event args for when user's speech has been recognized
/// </summary>
/// <param name="result">Details of the speech recognized</param>
public class SpeechRecognitionResultEventArgs(SpeechRecognitionResult result) : EventArgs
{
    /// <summary>
    /// Details of the speech recognized
    /// </summary>
    public SpeechRecognitionResult Result => result;
}