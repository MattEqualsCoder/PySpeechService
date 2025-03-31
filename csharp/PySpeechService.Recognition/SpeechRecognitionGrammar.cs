using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

[assembly: InternalsVisibleTo("PySpeechService.Client")]
namespace PySpeechService.Recognition;

/// <summary>
/// Built object housing a grammar rule to be used for speech recognition
/// </summary>
public class SpeechRecognitionGrammar
{
    internal GrammarElement RuleGrammarElement { get; }

    internal SpeechRecognitionGrammar(GrammarElement element)
    {
        RuleGrammarElement = element;
    }

    /// <summary>
    /// The name of the rule
    /// </summary>
    public string? RuleName
    {
        get => RuleGrammarElement.Key;
        set => RuleGrammarElement.Key = value;
    }

    /// <summary>
    /// Returns a list of strings for displaying to the user what phrases can be
    /// stated for this particular roll
    /// </summary>
    public ICollection<string> HelpText => RuleGrammarElement.GetHelpText();

    // Event for this rule has been recognized by speech recognized
    public event EventHandler<SpeechRecognitionResultEventArgs>? SpeechRecognized;

    /// <summary>
    /// Converts the grammar into a native System.Speech grammar object to be used
    /// with Windows built-in speech recognition
    /// </summary>
    /// <returns>The generated System.Speech grammar object</returns>
    [SupportedOSPlatform("windows")]
    public System.Speech.Recognition.Grammar BuildSystemSpeechGrammar()
    {
        System.Speech.Recognition.GrammarBuilder builder = new();
        RuleGrammarElement.AddToNativeGrammar(builder);
        
        var grammar = new System.Speech.Recognition.Grammar(builder)
        {
            Name = RuleName,
        };

        grammar.SpeechRecognized += (_, args) =>
        {
            OnSpeechRecognized(args.Result.Text, args.Result.Confidence,
                args.Result.Semantics.ToDictionary(x => x.Key,
                    x => new SpeechRecognitionSemantic(x.Key, x.Value.Value as string ?? "")), args.Result);
        };
        
        return grammar;
    }
    
    internal void OnSpeechRecognized(string text, float confidence, Dictionary<string, SpeechRecognitionSemantic>? semantics = null,
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
}