// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PySpeechServiceClient;
using PySpeechServiceClient.Grammar;
using PySpeechServiceClient.Models;
using Serilog;

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

var client = serviceProvider.GetRequiredService<IPySpeechService>();
await client.StartAsync();

List<SpeechRecognitionGrammar> rules = [];

var builder = new SpeechRecognitionGrammarBuilder("test rule 1");
builder.Append("Hey Tracker, ")
    .OneOf("how are you?", "fuck you");
var rule = builder.BuildGrammar();
rule.SpeechRecognized += (sender, eventArgs) =>
{
    Console.WriteLine($"Test rule 1 recognized: {eventArgs.Result.Text} ({eventArgs.Result.Confidence})");
};
client.AddSpeechRecognitionCommand(rule);

builder = new SpeechRecognitionGrammarBuilder("test rule 2");
builder.Append("Hey Tracker, ")
    .OneOf("where is my cat?", "are you a kitty cat?");
rule = builder.BuildGrammar();
rule.SpeechRecognized += (sender, eventArgs) =>
{
    Console.WriteLine($"Test rule 2 recognized: {eventArgs.Result.Text} ({eventArgs.Result.Confidence})");
};
client.AddSpeechRecognitionCommand(rule);

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
        await client.SpeakAsync(message);
    }
    
}

Thread.Sleep(TimeSpan.FromSeconds(6));