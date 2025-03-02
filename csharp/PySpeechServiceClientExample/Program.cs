// See https://aka.ms/new-console-template for more information

using System.Speech.Recognition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PySpeechServiceClient;
using PySpeechServiceClient.Grammar;
using PySpeechServiceClient.Models;
using Serilog;
using JsonSerializer = System.Text.Json.JsonSerializer;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var serviceProvider = new ServiceCollection()
    .AddLogging(loggingBuilder =>
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddSerilog();
    })
    .AddPySpeechService()
    .BuildServiceProvider();

List<SpeechRecognitionGrammar> rules = [];

var builder = new SpeechRecognitionGrammarBuilder("test rule 1");
builder.Append("Hey Tracker, ")
    .OneOf("how are you?", "fuck you");
var rule = builder.BuildGrammar();
var help1 = rule.HelpText.ToList();
rule.SpeechRecognized += (sender, eventArgs) =>
{
    Console.WriteLine($"Test rule 1 recognized: {eventArgs.Result.Text} ({eventArgs.Result.Confidence})");
};
rules.Add(rule);

builder = new SpeechRecognitionGrammarBuilder("test rule 2");
builder.Append("Hey Tracker, ")
    .Optional("can you tell me", "could you kindly tell me")
    .OneOf("where is my cat?", "are you a kitty cat?");

rule = builder.BuildGrammar();
var help2 = rule.HelpText.ToList();
rule.SpeechRecognized += (sender, eventArgs) =>
{
    Console.WriteLine($"Test rule 2 recognized: {eventArgs.Result.Text} ({eventArgs.Result.Confidence})");
};
rules.Add(rule);


builder = new SpeechRecognitionGrammarBuilder("test rule 3");
builder.Append("Hey Tracker, please give me")
    .Append("food", [
        new GrammarKeyValueChoice("Soup", "soup"),
        new GrammarKeyValueChoice("Pizza", "pizza"),
        new GrammarKeyValueChoice("Bread", "bread"),
        new GrammarKeyValueChoice("Fruit", "fruit"),
    ]);
rule = builder.BuildGrammar();

var help3 = rule.HelpText.ToList();
rule.SpeechRecognized += (sender, eventArgs) =>
{
    Console.WriteLine($"Test rule 3 recognized: {eventArgs.Result.Text} ({eventArgs.Result.Confidence}) - food: {eventArgs.Result.Semantics["food"].Value}");
};
rules.Add(rule);

var builder1 = new SpeechRecognitionGrammarBuilder();
builder1.Append("Hey Tracker, ")
    .OneOf("give me", "throw me")
    .OneOf("a bone", "an item");
var builder2 = new SpeechRecognitionGrammarBuilder();
builder2.Append("Hey Tracker, ")
    .OneOf("take", "steal")
    .OneOf("a bone", "an item");

builder = SpeechRecognitionGrammarBuilder.Combine(builder1, builder2);
rule = builder.BuildGrammar("test rule 4");
var help4 = rule.HelpText.ToList();
rule.SpeechRecognized += (sender, eventArgs) =>
{
    Console.WriteLine($"Test rule 4 recognized: {eventArgs.Result.Text} ({eventArgs.Result.Confidence})");
};
rules.Add(rule);

if (OperatingSystem.IsWindows())
{
    SpeechRecognitionEngine recognizer = new();
    recognizer.SetInputToDefaultAudioDevice();
        
    foreach (var ruleToAdd in rules)
    {
        var systemSpeech = ruleToAdd.BuildSystemSpeechGrammar();
        recognizer.LoadGrammar(systemSpeech);
    }

    recognizer.SpeechRecognized += (sender, eventArgs) =>
    {
        Console.WriteLine("Recognizier speech recognized " + JsonSerializer.Serialize(eventArgs.Result));
    };

    recognizer.RecognizeAsync(RecognizeMode.Multiple);

    Console.ReadLine();
    return;
}

var client = serviceProvider.GetRequiredService<IPySpeechService>();
client.AutoReconnect = true;
await client.StartAsync();
foreach (var ruleToAdd in rules)
{
    client.AddSpeechRecognitionCommand(ruleToAdd);
}

client.AddSpeechRecognitionReplacements(new Dictionary<string, string>()
{
    { "a pineapple", "fruit" },
    { "an orange", "fruit" },
});

client.SpeakCommandResponded += async (sender, eventArgs) =>
{
    List<string> parts = [];
    if (eventArgs.Response.IsStartOfMessage)
    {
        parts.Add("Message start");
    }

    if (eventArgs.Response.IsStartOfChunk)
    {
        parts.Add($"Chunk ({eventArgs.Response.CurrentChunk}) started");
    }

    if (eventArgs.Response.IsEndOfChunk)
    {
        parts.Add($"Chunk ({eventArgs.Response.CurrentChunk}) ended");
    }

    if (eventArgs.Response.IsEndOfMessage)
    {
        parts.Add("Message end");
    }

    Console.WriteLine(string.Join(" | ", parts));
};

while (client.IsConnected)
{
    Console.Write("Enter a phrase to state: ");
    var message = Console.ReadLine();
    if (string.IsNullOrEmpty(message) || message.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
    {
        await client.ShutdownAsync();
        client.Dispose();
    }
    else if (message.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
             message.Equals("shutup", StringComparison.OrdinalIgnoreCase))
    {
        await client.StopSpeakingAsync();
    }
    else if ("start speech recognition".Equals(message, StringComparison.OrdinalIgnoreCase))
    {
        await client.StartSpeechRecognitionAsync();
    }
    else if ("set defaults".Equals(message, StringComparison.OrdinalIgnoreCase))
    {
        Console.Write("Enter the model name: ");
        var modelName = Console.ReadLine() ?? "hfc_female";
        
        Console.Write("Enter the alt model name: ");
        var altModelName = Console.ReadLine() ?? "hfc_male";
        
        Console.Write("Enter the default speed (.5 - 2): ");
        double.TryParse(Console.ReadLine() ?? "", out var speed);
        
        Console.Write("Enter the default gain (-100, 100): ");
        double.TryParse(Console.ReadLine() ?? "", out var gain);
        
        Console.Write("Enter the default pitch (0.5 - 1.5): ");
        double.TryParse(Console.ReadLine() ?? "", out var pitch);

        await client.SetSpeechSettingsAsync(new SpeechSettings()
        {
            ModelName = modelName,
            AltModelName = altModelName,
            Speed = speed,
            Gain = gain,
            Pitch = pitch,
        });
    }
    else
    {
        if (message.StartsWith("sync "))
        {
            client.Speak(message);
            Console.WriteLine("Done");
        }
        else
        {
            await client.SpeakAsync(message);
        }
        
    }
    
}

Thread.Sleep(TimeSpan.FromSeconds(6));