# C# PySpeechService Client

The PySpeechServiceClient nuget package can be used to start and initialize PySpeechService, create speech recognition grammar, send messages to the PySpeechService, and receive messages from the PySpeechService application.

## Adding PySpeechServiceClient

To utilize PySpeechService in your C# application, first add the PySpeechServiceClient nuget package to your application. 

If you're using dependency injection, you can simply call the IServiceCollection extension function AddPySpeechService to add the relevant classes. You can get IPySpeechService via dependency injection wherever you need to use PySpeechService.

If you're not using dependency injection, you can create an instance of the PySpeechServiceBuilder class. You can add an optional logger with the `AddLogger(ILogger<IPySpeechService> logger)` function if you'd like the service to log messages. Then, you use the Build function to get the IPySpeechService object.

Once you have the IPySpeechService object, you can call `service.StartAsync()`. This will take a look at the various different potential locations and methods of running the PySpeechService application, start it, and then make sure it's connected and running.

Once the service has fully started and is running, the `Initialized` event on the service will be fired.

## Using Text to Speech

### Step 1: Set Default Speech Settings

To use text to speech, you first have to set the default speech settings for TTS. For this, you need to make the following call:

```
await client.SetSpeechSettingsAsync(new SpeechSettings()
{
    ModelName = modelName,
    AltModelName = altModelName,
    Speed = speed,
    Gain = gain,
    Pitch = pitch,
});
```

Model name and alt model names are [Piper TTS model](https://github.com/rhasspy/piper/blob/master/VOICES.md) name or path. The alt model name is used if you use a SSML voice tag (the provided voice doesn't matter - it will always use the same alt voice name). By setting speech settings, any Piper TTS models that are needed will be downloaded.

Speed, gain, and pitch modify the starting values before applying any SSML tags.

Once initialization has been completed, the `TextToSpeechInitialized` event will be fired.

### Step 2: Make Speak Calls

There are two varieties speak calls you can make:

```
client.Speak(message);
await client.SpeakAsync(message);
```

The first version will pause execution while PySpeechService is speaking whereas SpeakAsync will send the message request to the PySpeechService application (even if you await it). 

As the first parameter, you pass in a string message which can either be plain text or include basic SSML tags. You can optionally pass in a SpeechSettings class parameter if you want to modify how the voice will sound for a particular line.

### Step 3: Receive Speak Updates

The PySpeechService service has an event on it that will inform you of updates as the text to speech is progressing:

```
client.SpeakCommandResponded += async (sender, eventArgs) =>
{
    if (eventArgs.Response.IsStartOfMessage)
    {
        // Start of a message request that was sent to PySpeechService
    }

    if (eventArgs.Response.IsStartOfChunk)
    {
        // Start of an individual line or sentence of a request
    }

    if (eventArgs.Response.IsEndOfChunk)
    {
        // End of an individual line or sentence of a request
    }

    if (eventArgs.Response.IsEndOfMessage)
    {
        // End of a message request that was sent to PySpeechService
    }
};
```

When a request is sent to the PySpeechService, messages are broken into different "chunks" to make it easier to stack different SSML values as well as make it faster as large paragraphs of text could take a bit to generate the audio for. While PySpeechService is speaking, you will get these updates for each message and chunk that is stated.

### Step 4: Other Commands

```
// Stops PySpeechService from finishing its current queued lines
service.StopSpeakingAsync();

// Sets the volume of text to speech (0 - 2 with 0 being mute, 1
// being the default volume, and 2 being twice as loud)
service.SetVolumeAsync()
```

## Using Speech Recognition

### Step 1: Creating Grammar Rules

By creating a SpeechRecognitionGrammarBuilder for each rule, you can add the different statements that are used to create all of the phrases that will be listened for.

```
// Create builder for rule named "start app"
var builder = new SpeechRecognitionGrammarBuilder("start app");

// Adds a single static string
builder.Append("Hey computer,")

    // Adds optional phrases that the user can include
    .Optional("please", "can you please")

    // Adds phrases the user must pick between
    .OneOf("run", "start", "execute");

    // Adds options that will be returned as semantics in the response
    .Append("app", [
        new GrammarKeyValueChoice("Calculator", "calc"),
        new GrammarKeyValueChoice("Firefox", "firefox"),
        new GrammarKeyValueChoice("Chrome", "chrome"),
    ]); 

// Create the rule with the grammar
var rule = builder.BuildGrammar();
```

All parts added to the builder will be combined together to create the phrases to listen for. For example, the above code would listen to "Hey computer, run Calculator", "Hey computer, start Calculator", "Hey computer, execute Firefox", "Hey computer, can you please run Chrome", etc.

If you have two different sets of grammar you'd like to be joined into a single rule, you can use SpeechRecognitionGrammarBuilder.Combine to combine two different builders. The following will accept phrases like "Hey computer, pizza is my favorite food" as well as "Hey computer, my favorite food is pizza". It'll return the correct rule and semantics no matter how the user says it.

```
var builder1 = new SpeechRecognitionGrammarBuilder();
builder1.Append("Hey computer,")
    .Append("food", [
        new GrammarKeyValueChoice("Fruit", "fruit"),
        new GrammarKeyValueChoice("Pizza", "pizza"),
    ])
    .Append("is my favorite food");

var builder2 = new SpeechRecognitionGrammarBuilder();
builder2.Append("Hey computer,")
    .Append("my favorite food is")
    .Append("food", [
        new GrammarKeyValueChoice("Fruit", "fruit"),
        new GrammarKeyValueChoice("Pizza", "pizza"),
    ]);

builder = SpeechRecognitionGrammarBuilder.Combine(builder1, builder2);
rule = builder.BuildGrammar("Favorite Food Rule");
```

For additional details and examples of building grammar, please see the grammar JSON documentation.

Once you have your rule built, you then need to add it to the service:

```
service.AddSpeechRecognitionCommand(rule);
```

### Step 2: Handling Results

Each rule has a SpeechRecognized event that can be used to handle what happens when a rule is recognized by PySpeechService.

```
rule.SpeechRecognized += (sender, eventArgs) =>
{
    Console.WriteLine($"Recognized text: {eventArgs.Result.Text}");
    Console.WriteLine($"Confidence: {eventArgs.Result.Confidence}");
    Console.WriteLine($"Semantics: {string.Join(", ", eventArgs.Result.Semantics.Select(x => $"{x.Key}={x.Value}"))}");
};
```

### Step 4: Speech Recognition Replacements

Sometimes you may want to use grammar key value choices that may be hard for speech recognition to understand. For example, fantasy or made up words may not work with speech recognition. In those cases, you may need to use a combination of English words. For example, for "Gondor" you can have it listen for "gone door" instead.

For this, you can use the AddSpechRecognitionReplacements function, which takes in a dictionary where the key is the phrase that speech recognition will listen for and the value is the value of the GrammarKeyValueChoice created for speech recognition. When you get the speech recognition response, the value here will be returned instead of the key.

```
service.AddSpeechRecognitionReplacements(new Dictionary<string, string>()
{
    { "gone door", "Gondor" },
    { "eyes in guard", "Isengard" },
});
```

### Step 5: Starting and Stopping Speech Recognition

When you have all of your rules added, you can then start speech recognition with the following command:

```
await client.StartSpeechRecognitionAsync(
    voskModel: "", 
    prefix: "Hey computer", 
    requiredConfidence: 80
);
```

**voskModel**: This is the [VOSK model](https://alphacephei.com/vosk/models) that will be used for speech recognition. By default the English small model is used.
**prefix**: If all of your rules start with the exact same phrase, you can use the prefix to have PySpeechRecognition check that the spoken phrase starts with that phrase independently of checking the full phrase. This can help prevent false positives.
**requiredConfidence**: This is a number from 0 - 100 that represents the confidence required that the phrase is what the user said. Note that VOSK itself does not return a confidence, so this is just the confidence that what VOSK heard is the matched phrase by PySpeechService.

When speech recognition has been fully initialized, you will receive a `SpeechRecognitionInitialized` event. 

If you want to stop speech recognition, simply call the `service.StopSpeechRecognitionAsync` function.

## Native Windows Speech Recognition

In case you want to use the native C# System.Speech speech recognition for users on Windows, you can use the SpeechRecognitionGrammarBuilder to build a series of rules and then load them into the System.Speech SpeechRecognitionEngine with the BuildSystemSpeechGrammar function.

```
SpeechRecognitionEngine recognizer = new();
recognizer.SetInputToDefaultAudioDevice();
    
foreach (var ruleToAdd in rules)
{
    var systemSpeech = ruleToAdd.BuildSystemSpeechGrammar();
    recognizer.LoadGrammar(systemSpeech);
}
```