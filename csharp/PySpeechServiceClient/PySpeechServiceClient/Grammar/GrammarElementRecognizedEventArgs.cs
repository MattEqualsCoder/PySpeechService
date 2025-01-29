using System.Runtime.Versioning;
using System.Speech.Recognition;

namespace PySpeechServiceClient.Grammar;

public class GrammarElementRecognizedEventArgs
{
    public string HeardText { get; set; } = string.Empty;
    public string RecognizedText { get; set; } = string.Empty;
    public Dictionary<string, object>? Semantics { get; set; }

    private object? _nativeRecognitionResult;

    [SupportedOSPlatform("windows")]
    public RecognitionResult? GetNativeRecognitionResult()
    {
        return _nativeRecognitionResult as RecognitionResult;
    }
    
    [SupportedOSPlatform("windows")]
    internal void SetNativeRecognitionResult(RecognitionResult recognitionResult)
    {
        _nativeRecognitionResult = recognitionResult;
    }
}