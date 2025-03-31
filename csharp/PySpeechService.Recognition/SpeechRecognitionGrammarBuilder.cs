namespace PySpeechService.Recognition;

/// <summary>
/// Builder for generating speech recognition grammar that can be used for either Windows speech
/// recognition or the PySpeechService speech recognition
/// </summary>
public class SpeechRecognitionGrammarBuilder
{
    private readonly GrammarElement _grammarElement = new(GrammarElementType.Rule);
    private readonly List<GrammarElement> _grammarElements = [];
    
    /// <summary>
    /// Constructor for creating a builder for a specified rule name
    /// </summary>
    /// <param name="rule">Rule name for this grammar</param>
    public SpeechRecognitionGrammarBuilder(string? rule = null)
    {
        _grammarElement.Key = string.IsNullOrEmpty(rule) ? Guid.NewGuid().ToString() : rule;
        _grammarElement.Data = _grammarElements;
    }

    /// <summary>
    /// Constructor for creating a builder for a specified rule name
    /// </summary>
    /// <param name="choices">Other grammar builder to combine into this grammar object</param>
    /// <param name="rule">Rule name for this grammar</param>
    public SpeechRecognitionGrammarBuilder(IEnumerable<SpeechRecognitionGrammarBuilder> choices, string? rule = null)
    {
        _grammarElement.Key = rule;
        _grammarElement.Data = _grammarElements;
        _grammarElements.Add(new GrammarElement(GrammarElementType.GrammarElementList, choices
            .Select(builder => new GrammarElement(GrammarElementType.Rule, builder._grammarElements)).ToList()));
    }
    
    /// <summary>
    /// Combine multiple grammar builders into a single one
    /// </summary>
    /// <param name="choices">The different grammar builders to combine into a single rule</param>
    /// <returns>The combined builder object</returns>
    public static SpeechRecognitionGrammarBuilder Combine(params SpeechRecognitionGrammarBuilder[] choices)
    {
        return new SpeechRecognitionGrammarBuilder(choices);
    }
    
    /// <summary>
    /// Combine multiple grammar builders into a single one
    /// </summary>
    /// <param name="ruleName">The name of the rule of the combined builder</param>
    /// <param name="choices">The different grammar builders to combine into a single rule</param>
    /// <returns>The combined builder object</returns>
    public static SpeechRecognitionGrammarBuilder Combine(string? ruleName, params SpeechRecognitionGrammarBuilder[] choices)
    {
        return new SpeechRecognitionGrammarBuilder(choices, ruleName);
    }
    
    /// <summary>
    /// Adds a required string to the grammar
    /// </summary>
    /// <param name="text">The text to add</param>
    /// <returns>The updated builder object</returns>
    public SpeechRecognitionGrammarBuilder Append(string text)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.String, text));
        return this;
    }
    
    /// <summary>
    /// Adds a required choice that the user will have to pick from
    /// </summary>
    /// <param name="key">The key for looking up the user selected</param>
    /// <param name="grammarChoices">The options the user can pick from</param>
    /// <returns>The updated builder object</returns>
    public SpeechRecognitionGrammarBuilder Append(string key, List<GrammarKeyValueChoice> grammarChoices)
    {
        
        _grammarElements.Add(new GrammarElement(GrammarElementType.KeyValue, grammarChoices, key));
        return this;
    }
    
    /// <summary>
    /// Adds a required set of phrases the user will have to pick from
    /// </summary>
    /// <param name="choices">The choices the user will have to pick from</param>
    /// <returns>The updated builder object</returns>
    public SpeechRecognitionGrammarBuilder OneOf(params string[] choices)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.OneOf, choices));
        return this;
    }
    
    /// <summary>
    /// Adds an optional phrase for the user to say
    /// </summary>
    /// <param name="text">The phrase the user can say</param>
    /// <returns>The updated builder object</returns>
    public SpeechRecognitionGrammarBuilder Optional(string text)
    {
        return Optional([text]);
    }
    
    /// <summary>
    /// Adds an optional set of phrases the user can pick from
    /// </summary>
    /// <param name="choices">The phrases the user can pick from</param>
    /// <returns>The updated builder object</returns>
    public SpeechRecognitionGrammarBuilder Optional(params string[] choices)
    {
        _grammarElements.Add(new GrammarElement(GrammarElementType.Optional, choices));
        return this;
    }

    /// <summary>
    /// Builds a SpeechRecognitionGrammar object
    /// </summary>
    /// <param name="ruleName">The name of the rule to assign the builder object to</param>
    /// <returns>The generated grammar</returns>
    public SpeechRecognitionGrammar BuildGrammar(string? ruleName = null)
    {
        if (!string.IsNullOrEmpty(ruleName))
        {
            _grammarElement.Key = ruleName;
        }
        
        return new SpeechRecognitionGrammar(_grammarElement);
    }
}