using System.Runtime.Versioning;

namespace PySpeechServiceClient.Grammar;

public class GrammarBuilder
{
    public readonly GrammarElement GrammarElement = new(GrammarElementType.Rule);

    private readonly List<GrammarElement> _grammarElements = [];
    
    public GrammarBuilder(string? rule = null)
    {
        GrammarElement.Key = rule;
        GrammarElement.Data = _grammarElements;
    }

    public GrammarBuilder(IEnumerable<GrammarBuilder> choices, string? rule = null)
    {
        GrammarElement.Key = rule;
        GrammarElement.Data = _grammarElements;
        _grammarElements.Add(new GrammarElement(GrammarElementType.GrammarElementList, choices
            .Select(builder => new GrammarElement(GrammarElementType.Rule, builder._grammarElements)).ToList()));
    }
    
    public static GrammarBuilder Combine(params GrammarBuilder[] choices)
    {
        return new GrammarBuilder(choices);
    }
    
    public GrammarBuilder Append(string text)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.String, text));
        return this;
    }
    
    public GrammarBuilder Append(string key, List<GrammarKeyValueChoice> grammarChoices)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.KeyValue, grammarChoices));
        return this;
    }
    
    public GrammarBuilder OneOf(params string[] choices)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.OneOf, choices));
        return this;
    }
    
    public GrammarBuilder Optional(string text)
    {
        return Optional([text]);
    }
    
    public GrammarBuilder Optional(params string[] choices)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.Optional, choices));
        return this;
    }

    [SupportedOSPlatform("windows")]
    public System.Speech.Recognition.Grammar Build()
    {
        System.Speech.Recognition.GrammarBuilder builder = new(GrammarElement.Key);
        GrammarElement.AddToNativeGrammar(builder);
        return new System.Speech.Recognition.Grammar(builder)
        {
            Name = GrammarElement.Key
        };
    }
}