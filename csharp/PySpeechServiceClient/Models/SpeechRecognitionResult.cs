namespace PySpeechServiceClient.Models;


public class SpeechRecognitionSemantic(string key, string value)
{
    public string Key => key;
    public string Value => value;
}

public class SpeechRecognitionResult : EventArgs
{
    public required string Text { get; set; }
    public required float Confidence { get; set; }
    public Dictionary<string, SpeechRecognitionSemantic> Semantics { get; set; } = [];
    
    public System.Speech.Recognition.RecognitionResult? NativeResult { get; set; }
}

public class SpeechRecognitionResultEventArgs(SpeechRecognitionResult result) : EventArgs
{
    public SpeechRecognitionResult Result => result;
}