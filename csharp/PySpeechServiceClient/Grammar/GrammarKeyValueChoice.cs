namespace PySpeechServiceClient.Grammar;

/// <summary>
/// A class for an option that can be used when building grammar
/// </summary>
/// <param name="listenPhrase">The phrase to listen for</param>
/// <param name="value">The value to return in the semantics. The listen phrase will be returned if null.</param>
public class GrammarKeyValueChoice(string listenPhrase, object? value = null)
{
    public string Key { get; set; } = listenPhrase;
    public object Value { get; set; } = value ?? listenPhrase;
}