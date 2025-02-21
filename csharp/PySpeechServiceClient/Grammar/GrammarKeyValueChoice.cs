namespace PySpeechServiceClient.Grammar;

public class GrammarKeyValueChoice(string key, object? value = null)
{
    public string Key { get; set; } = key;
    public object Value { get; set; } = value ?? key;
}