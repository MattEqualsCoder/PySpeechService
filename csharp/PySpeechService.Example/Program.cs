using System.Speech.Recognition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PySpeechService.Client;
using PySpeechService.Recognition;
using PySpeechService.TextToSpeech;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var serviceCollection = new ServiceCollection()
    .AddLogging(loggingBuilder =>
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddSerilog();
    });

// Add PySpeechService for dependency injection
// Currently only Linux is supported
if (OperatingSystem.IsLinux())
{
    serviceCollection = serviceCollection.AddPySpeechService();
}
    
var serviceProvider = serviceCollection.BuildServiceProvider();

#region Create Grammar Rules
List<SpeechRecognitionGrammar> rules = [];

// Rule that listens for "Hey computer, what is my CPU usage?" and "Hey computer, how much CPU am I using?"
var builder = new SpeechRecognitionGrammarBuilder("CPU Usage Rule");
builder.Append("Hey computer,")
    .OneOf("what is my CPU usage?", "how much CPU am I using?");
var rule = builder.BuildGrammar();
rule.SpeechRecognized += (_, eventArgs) =>
{
    Console.WriteLine($"CPU Usage Rule identified: {eventArgs.Result.Text} ({eventArgs.Result.Confidence})");
    Console.Write("Enter command: ");
};
rules.Add(rule);

// Rule that listens for memory usage where the user can include "can you tell me" or "could you kindly tell me",
// but it is not required. For example, "Hey computer, can you tell me what is my memory usage?"
builder = new SpeechRecognitionGrammarBuilder("Memory Usage Rule");
builder.Append("Hey computer, ")
    .Optional("can you tell me", "could you kindly tell me")
    .OneOf("what is my memory usage?", "what is my RAM usage?");
rule = builder.BuildGrammar();
rule.SpeechRecognized += (_, eventArgs) =>
{
    Console.WriteLine($"Memory Usage Rule recognized: {eventArgs.Result.Text} ({eventArgs.Result.Confidence})");
    Console.Write("Enter command: ");
};
rules.Add(rule);

// Rule that listens for "Hey computer, launch" followed by either "Mozilla Firefox", "Google Chrome", or "Steam".
// In the response, the semantics object will have a key value pair of "application" as the key and either 
// "firefox", "chrome", or "steam" based on what the user said.
builder = new SpeechRecognitionGrammarBuilder("Launch Application Rule");
builder.Append("Hey computer, launch")
    .Append("application", [
        new GrammarKeyValueChoice("Mozilla Firefox", "firefox"),
        new GrammarKeyValueChoice("Google Chrome", "chrome"),
        new GrammarKeyValueChoice("Steam", "steam"),
        new GrammarKeyValueChoice("RetroArch", "retroarch"),
    ]);
rule = builder.BuildGrammar();
rule.SpeechRecognized += (_, eventArgs) =>
{
    var selectedApplication = eventArgs.Result.Semantics["application"].Value;
    Console.WriteLine($"Launch Application Rule recognized with the selected application of: {selectedApplication}");
    Console.Write("Enter command: ");
};
rules.Add(rule);

// Rule that combines two different sets of grammar with the same key value options, allowing the structure of
// the phrases to be different. For example, "Hey computer, set Mozilla Firefox as my default browser" vs "Hey
// computer, set my default browser to Google Chrome".
var builder1 = new SpeechRecognitionGrammarBuilder();
builder1.Append("Hey computer, set")
    .Append("browser", [
        new GrammarKeyValueChoice("Mozilla Firefox", "firefox"),
        new GrammarKeyValueChoice("Google Chrome", "chrome"),
    ])
    .OneOf("as my default browser", "as my preferred browser");
var builder2 = new SpeechRecognitionGrammarBuilder();
builder2.Append("Hey computer,")
    .OneOf("set my default browser to", "update my default browser as")
    .Append("browser", [
        new GrammarKeyValueChoice("Mozilla Firefox", "firefox"),
        new GrammarKeyValueChoice("Google Chrome", "chrome"),
    ]);
builder = SpeechRecognitionGrammarBuilder.Combine(builder1, builder2);
rule = builder.BuildGrammar("Default Browser Rule");
rule.SpeechRecognized += (_, eventArgs) =>
{
    var selectedApplication = eventArgs.Result.Semantics["browser"].Value;
    Console.WriteLine($"Default Browser Rule recognized with the selected application of: {selectedApplication}");
    Console.Write("Enter command: ");
};
rules.Add(rule);
#endregion

#region System.Speech SpeechRecognitionEngine Support
// On Windows, you can use the above grammar with the System.Speech SpeechRecognitionEngine class
if (OperatingSystem.IsWindows())
{
    SpeechRecognitionEngine recognizer = new();
    recognizer.SetInputToDefaultAudioDevice();
        
    // Go through the rules generated and call the BuildSystemSpeechGrammar function to generate a
    // System.Speech.Recognition.Grammar that can be loaded into the SpeechRecognitionEngine
    foreach (var ruleToAdd in rules)
    {
        var systemSpeech = ruleToAdd.BuildSystemSpeechGrammar();
        recognizer.LoadGrammar(systemSpeech);
    }

    recognizer.RecognizeAsync(RecognizeMode.Multiple);

    Console.ReadLine();
    return;
}
#endregion

if (!OperatingSystem.IsLinux())
{
    Console.WriteLine("Currently only Linux is supported");
    return;
}

#region Service Initialization
// You can get the IPySpeechService by either using dependency injection or by using PySpeechServiceBuilder
IPySpeechService service;

// Builder example
{
    var serviceBuilder = new PySpeechServiceBuilder();
    // serviceBuilder.AddLogger(serviceProvider.GetRequiredService<ILogger<IPySpeechService>>());
    service = serviceBuilder.Build();
}

// Dependency injection example
{
    service = serviceProvider.GetRequiredService<IPySpeechService>();
}

// Add all the speech recognition rules
foreach (var ruleToAdd in rules)
{
    service.AddSpeechRecognitionCommand(ruleToAdd);
}
#endregion

#region Events
// For non-standard and fictional words, the speech recognition engine may now know how to listen for it. For key
// value pairs, you can use this speech recognition replacements to provide substitution where the key is the
// phrase to listen for, and the value is the value to replace it with when returned back to your application
service.AddSpeechRecognitionReplacements(new Dictionary<string, string>()
{
    { "retro arc", "RetroArch" },
});

// Event called once the PySpeechService application has been started and connected to successfully
service.Initialized += (_, _) =>
{
    Console.WriteLine("PySpeechService initialized and connection confirmed");
};

// Event called once Piper and all needed files have been downloaded and are ready after SetSpeechSettingsAsync
// has been called
service.TextToSpeechInitialized += (_, _) =>
{
    Console.WriteLine($"Piper TextToSpeech initialized");
    Console.Write("Enter command: ");
};

// Event called once speech recognition has been initialized successfully after StartSpeechRecognitionAsync has been
// called. In the returned event args, a list of words that couldn't be recognized by the VOSK speech recognition
// engine. You will either need to reword things or, for key value pairs, use AddSpeechRecognitionReplacements.
service.SpeechRecognitionInitialized += (_, eventArgs) =>
{
    Console.WriteLine("VOSK speech recognition initialized");
    if (eventArgs.InvalidSpeechRecognitionWords?.Count > 0)
    {
        var invalidWords = string.Join(", ", eventArgs.InvalidSpeechRecognitionWords ?? []);
        Console.WriteLine($"The following words do not work with VOSK: {invalidWords}");
    }
    Console.Write("Enter command: ");
};

// Event called as an update of how text to speech is progressing. Paragraphs and SSML requests are broken into
// chunks to make processing faster. Each sentence is its own chunk, and each SSML tag will create new chunks.
service.SpeakCommandResponded += (_, eventArgs) =>
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

    if (eventArgs.Response.IsEndOfMessage)
    {
        Console.Write("Enter command: ");
    }
};

// Event for whenever any speech is successfully recognized
service.SpeechRecognized += (_, eventArgs) =>
{
    Console.WriteLine($"Sentence recognized: {eventArgs.Result.Text}");
    Console.Write("Enter command: ");
};
#endregion

#region Start Service and Initiate gRPC Text to Speech & Speech Recognition Commands
// Start the PySpeechService application and connect to it
service.AutoReconnect = true;
await service.StartAsync();

PrintCommands();

while (service.IsConnected)
{
    Console.Write("Enter command: ");
    var message = Console.ReadLine() ?? "";
    
    // Sets the default text to speech settings and downloads piper and the TTS model if necessary
    if(IsSpeakSpeechSettingsRequest(message, out var modelName, out var altModelName, out var speed, out var gain, out var pitch))
    {
        await service.SetSpeechSettingsAsync(new SpeechSettings()
        {
            ModelName = modelName,
            AltModelName = altModelName,
            Speed = speed,
            Gain = gain,
            Pitch = pitch,
        });
    }
    
    // Starts text to speech for a message in either asynchronously or synchronously
    else if (IsSpeakRequest(message, out var toSpeak, out var isAsync))
    {
        if (!isAsync)
        {
            service.Speak(toSpeak);
            Console.WriteLine("Wait speak message complete");
        }
        else
        {
            await service.SpeakAsync(toSpeak);
        }
    }
    
    // Stops text to speech and clears the pending speech queue
    else if (IsStopTalkingRequest(message))
    {
        await service.StopSpeakingAsync();
    }
    
    // Sets the text to speech volume
    else if (IsSetVolumeRequest(message, out var volume))
    {
        await service.SetVolumeAsync(volume);
    }
    
    // Submits the grammar details to PySpeechService, downloads VOSK if needed, and starts speech recognition
    else if (IsStartSpeechRecognitionRequest(message))
    {
        await service.StartSpeechRecognitionAsync(voskModel: "", prefix: "Hey Computer", requiredConfidence: 80);
    }
    
    // Stops speech recognition. Use StartSpeechRecognitionAsync to restart it
    else if (IsStopSpeechRecognitionRequest(message))
    {
        await service.StopSpeechRecognitionAsync();
    }
    
    // Stops the PySpeechService application
    else if (IsShutdownRequest(message))
    {
        await service.ShutdownAsync();
        service.Dispose();
    }
    
    else
    {
        Console.WriteLine("Invalid command");
        PrintCommands();
    }
}
#endregion

Thread.Sleep(TimeSpan.FromSeconds(6));
return;

#region Console Input Functions
void PrintCommands()
{
    Console.WriteLine("Enter one of the following commands: ");
    Console.WriteLine("- set speech settings: Enter default speech settings and initialize text to speech");
    Console.WriteLine("- say/speak async <message>: Speaks the provided message and continues without waiting");
    Console.WriteLine("- say/speak wait <message>: Speaks the provided message and does not proceed until it is finished");
    Console.WriteLine("- stop/shutup: Stops text to speech and clears the current pending queue");
    Console.WriteLine("- set volume <number>: Sets the volume for text to speech");
    Console.WriteLine("- start speech recognition: Submits the rules to PySpeechService and starts speech recognition");
    Console.WriteLine("- stop speech recognition: Stops speech recognition. Use start speech recognition to restart");
    Console.WriteLine("- shutdown: Stops the PySpeechService application");
}

bool IsShutdownRequest(string message) => string.IsNullOrEmpty(message) || message.Equals("shutdown", StringComparison.OrdinalIgnoreCase);

bool IsStopTalkingRequest(string message) => message.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
                                              message.Equals("shutup", StringComparison.OrdinalIgnoreCase);

bool IsStartSpeechRecognitionRequest(string message) =>
    "start speech recognition".Equals(message, StringComparison.OrdinalIgnoreCase);

bool IsStopSpeechRecognitionRequest(string message) =>
    "stop speech recognition".Equals(message, StringComparison.OrdinalIgnoreCase);

bool IsSetVolumeRequest(string message, out double volume)
{
    if (message.StartsWith("set volume "))
    {
        if (double.TryParse(message.Replace("set volume ", ""), out volume))
        {
            return true;
        }
    }

    volume = 0;
    return false;
}

bool IsSpeakSpeechSettingsRequest(string message, out string modelName, out string altModelName, out double speed, out double gain, out double pitch)
{
    var command = message.ToLower();
    if ("set defaults" != command && "set speech settings" != command)
    {
        modelName = "";
        altModelName = "";
        speed = 0;
        gain = 0;
        pitch = 0;
        return false;
    }
    
    Console.Write("Enter the model name (hfc_female): ");
    modelName = Console.ReadLine() ?? "hfc_female";
        
    Console.Write("Enter the alt model name (hfc_male): ");
    altModelName = Console.ReadLine() ?? "hfc_male";
        
    Console.Write("Enter the default speed from .5 to 2 (1): ");
    _ = double.TryParse(Console.ReadLine() ?? "", out speed);
        
    Console.Write("Enter the default gain from -100 to 100 (0): ");
    _ = double.TryParse(Console.ReadLine() ?? "", out gain);
        
    Console.Write("Enter the default pitch from 0.5 - 1.5 (1): ");
    _ = double.TryParse(Console.ReadLine() ?? "", out pitch);

    return true;
}

bool IsSpeakRequest(string message, out string toSpeakMessage, out bool isAsync)
{
    var command = message.ToLower();
    if (command.StartsWith("say async "))
    {
        isAsync = true;
        toSpeakMessage = message[9..];
        return true;
    }
    else if (command.StartsWith("speak async "))
    {
        isAsync = true;
        toSpeakMessage = message[12..];
        return true;
    }
    if (command.StartsWith("say wait "))
    {
        isAsync = false;
        toSpeakMessage = message[9..];
        return true;
    }
    else if (command.StartsWith("speak wait "))
    {
        isAsync = false;
        toSpeakMessage = message[12..];
        return true;
    }
    
    toSpeakMessage = "";
    isAsync = false;
    return false;
}
#endregion