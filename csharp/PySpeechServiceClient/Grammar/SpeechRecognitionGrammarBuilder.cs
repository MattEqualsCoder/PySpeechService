using System.Runtime.Versioning;

namespace PySpeechServiceClient.Grammar;

public class SpeechRecognitionGrammarBuilder
{
    public readonly GrammarElement GrammarElement = new(GrammarElementType.Rule);

    private readonly List<GrammarElement> _grammarElements = [];
    
    public SpeechRecognitionGrammarBuilder(string? rule = null)
    {
        GrammarElement.Key = string.IsNullOrEmpty(rule) ? Guid.NewGuid().ToString() : rule;
        GrammarElement.Data = _grammarElements;
    }

    public SpeechRecognitionGrammarBuilder(IEnumerable<SpeechRecognitionGrammarBuilder> choices, string? rule = null)
    {
        GrammarElement.Key = rule;
        GrammarElement.Data = _grammarElements;
        _grammarElements.Add(new GrammarElement(GrammarElementType.GrammarElementList, choices
            .Select(builder => new GrammarElement(GrammarElementType.Rule, builder._grammarElements)).ToList()));
    }
    
    public static SpeechRecognitionGrammarBuilder Combine(params SpeechRecognitionGrammarBuilder[] choices)
    {
        return new SpeechRecognitionGrammarBuilder(choices);
    }
    
    public SpeechRecognitionGrammarBuilder Append(string text)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.String, text));
        return this;
    }
    
    public SpeechRecognitionGrammarBuilder Append(string key, List<GrammarKeyValueChoice> grammarChoices)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.KeyValue, grammarChoices));
        return this;
    }
    
    public SpeechRecognitionGrammarBuilder OneOf(params string[] choices)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.OneOf, choices));
        return this;
    }
    
    public SpeechRecognitionGrammarBuilder Optional(string text)
    {
        return Optional([text]);
    }
    
    public SpeechRecognitionGrammarBuilder Optional(params string[] choices)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.Optional, choices));
        return this;
    }

    public SpeechRecognitionGrammar BuildGrammar(string? ruleName = null)
    {
        if (!string.IsNullOrEmpty(ruleName))
        {
            GrammarElement.Key = ruleName;
        }
        
        return new SpeechRecognitionGrammar(GrammarElement);
    }
}