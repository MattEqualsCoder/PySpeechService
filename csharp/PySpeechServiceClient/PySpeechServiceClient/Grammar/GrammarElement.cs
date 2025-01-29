using System.Runtime.Versioning;
using System.Speech.Recognition;

namespace PySpeechServiceClient.Grammar;

public class GrammarElement(GrammarElementType type, object? data = null, string? key = null)
{
    public GrammarElementType Type { get; set; } = type;
    public string? Key { get; set; } = key;
    public object Data { get; set; } = data ?? string.Empty;

    [SupportedOSPlatform("windows")]
    public void AddToNativeGrammar(System.Speech.Recognition.GrammarBuilder grammarBuilder)
    {
        if (Type == GrammarElementType.Rule)
        {
            if (Data is not List<GrammarElement> elements)
            {
                throw new InvalidOperationException("Data must be a list of grammar elements.");
            }

            foreach (var element in elements)
            {
                element.AddToNativeGrammar(grammarBuilder);
            }
        }
        else if (Type == GrammarElementType.String)
        {
            if (Data is not string text)
            {
                throw new InvalidOperationException("Data must be a string.");
            }
            grammarBuilder.Append(text);
        }
        else if (Type == GrammarElementType.OneOf)
        {
            if (Data is not string[] choices)
            {
                throw new InvalidOperationException("Data must be a string array.");
            }
            grammarBuilder.Append(new Choices(choices));
        }
        else if (Type == GrammarElementType.Optional)
        {
            if (Data is not string[] choices)
            {
                throw new InvalidOperationException("Data must be a string array.");
            }
            grammarBuilder.Append(new Choices(choices), 0, 1);
        }
        else if (Type == GrammarElementType.KeyValue)
        {
            if (Data is not List<GrammarKeyValueChoice> choices)
            {
                throw new InvalidOperationException("Data must be a list of GrammarKeyValueChoices.");
            }

            var grammarBuilderChoices = new Choices();
            foreach (var choice in choices)
            {
                grammarBuilderChoices.Add(new SemanticResultValue(choice.Key, choice.Value));
            }
            grammarBuilder.Append(new SemanticResultKey(Key, grammarBuilderChoices));
        }
        else if (Type == GrammarElementType.GrammarElementList)
        {
            if (Data is not List<GrammarElement> elements)
            {
                throw new InvalidOperationException("Data must be a list of GrammarElements.");
            }

            List<System.Speech.Recognition.GrammarBuilder> subElementBuilders = [];
            foreach (var element in elements)
            {
                System.Speech.Recognition.GrammarBuilder subElementBuilder = new();
                element.AddToNativeGrammar(subElementBuilder);
                subElementBuilders.Add(subElementBuilder);
            }
            grammarBuilder.Append(new Choices(subElementBuilders.ToArray()));
        }
    }
}