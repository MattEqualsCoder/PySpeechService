using System.Runtime.Versioning;
using PySpeechServiceClient.Models;

namespace PySpeechServiceClient.Grammar;

public class SpeechRecognitionGrammar (GrammarElement element)
{
    public GrammarElement RuleGrammarElement => element;

    public string? RuleName
    {
        get => element.Key;
        set => element.Key = value;
    }

    public event EventHandler<SpeechRecognitionResultEventArgs>? SpeechRecognized;

    public void OnSpeechRecognized(string text, float confidence, Dictionary<string, SpeechRecognitionSemantic>? semantics = null,
        System.Speech.Recognition.RecognitionResult? nativeResult = null)
    {
        SpeechRecognized?.Invoke(this, new SpeechRecognitionResultEventArgs(new SpeechRecognitionResult()
        {
            Text = text,
            Confidence = confidence,
            Semantics = semantics ?? [],
            NativeResult = nativeResult   
        }));
    }
    
    [SupportedOSPlatform("windows")]
    public System.Speech.Recognition.Grammar BuildSystemSpeechGrammar()
    {
        System.Speech.Recognition.GrammarBuilder builder = new();
        RuleGrammarElement.AddToNativeGrammar(builder);
        
        var grammar = new System.Speech.Recognition.Grammar(builder)
        {
            Name = RuleName,
        };

        grammar.SpeechRecognized += (sender, args) =>
        {
            OnSpeechRecognized(args.Result.Text, args.Result.Confidence,
                args.Result.Semantics.ToDictionary(x => x.Key,
                    x => new SpeechRecognitionSemantic(x.Key, x.Value.Value as string ?? "")), args.Result);
        };
        
        return grammar;
    }
}