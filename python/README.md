# PySpeechService Developer Documentation

This documentation will cover utilizing the PySpeechService application for text-to-speech and speech recognition purposes in another application.

## Step 1: Generate gRPC Files

Use the [PySpeechService gRPC proto file](https://raw.githubusercontent.com/MattEqualsCoder/PySpeechService/refs/heads/main/python/speech_service.proto) to generate the files needed to utilize the service.

## Step 2: Launch the PySpeechService Application

Launch the PySpeechService application, keeping in mind that the application can be setup in multiple ways.

* Executable in path - Simply execute PySpeechService
* Python module - `python -m py-speech-service` or `python3 -m py-speech-service` or `py -m py-speech-service`
* Local app folder - `~/.local/share/py_speech_service/py-speech-service` or `%localappdata%/py_speech_service/py-speech-service.exe`

Once it's launched, read the application output. The first line is a JSON message to give information about PySpeechService as it waits for the a connection.

```
{
    "version": "0.1.0",
    "port": 12345
}
```

The version is the current version of the PySpeechService, which can be used to verify compatibility. The port is the random port used by the PySpeechService application for gRPC.

## Step 3: Send Requests

### Connect to the PySpeechService gRPC Channel

Using gRPC generated code and the standards for gRPC usage for the language of your application, connect to the PySpeechService channel and client, then call StartSpeechService. StartSpeechService is a two-way stream of SpeechServiceRequests and SpeechServiceResponses.

Use the stream to send SpeechServiceRequests to PySpeechService to initialize and use TTS and speech recognition. You'll then listen to the stream's SpeechServiceResponses to receive updates on when initialization is complete, when TTS starts and stops, and when speech has been recognized.

### Initialize TTS

Before you use TTS, you need to first send a request to PySpeechService informing it of the defaults to use for TTS. This allows it to do a few things. First, it'll tell PySpeechService to download any files necessary. Second, it gives it default information to use when sending text to use for TTS.

The following is an example of the request you can send:

```
{
    "set_speech_settings": {
        "speech_settings": {
            "model_name": "hfc_female",
            "alt_model_name": "hfc_male",
        }
    }
}
```

The model name is the name of a [Piper TTS model](https://github.com/rhasspy/piper/blob/master/VOICES.md). If you have an onnx and config file for a Piper voice, you can also pass that in as `onnx_path` and `config_path`. The alt details are used as any voice if you use the SSML voice tag.

### Speak via TTS

To request PySpeechService to speak a message, send the following request:

```
{
    "speak": {
        "message": "This is a new message.\nThis is the second line.",
        "speech_settings": {
            "model_name": "hfc_male",
            "pitch": 1.1
        }
    }
}
```

The message is either basic text, or it can include basic SSML for changing pitch, speed, or voice. You can include speech settings to modify the pitch, voice, speed, and other settings. Any speak requests sent while a message is being spoken, those requests will be sent to a queue.

### Set Speech Volume

You can update the default text to speech volume by calling making a set volume request:

```
{
    "set_volume": {
        "volume": .8
    }
}
```

The volume is a number from 0 to 2, 0 being muted, 1 being the default volume, and 2 being twice as loud as default.

### Stop Speaking

If you need to have the PySpeechService stop speaking, you can send the stop_speaking request:

```
{
    "stop_speaking": {}
}
```

### Initialize Speech Recognition

To initialize speech recognition, you need to first have grammar created. First you need to write a JSON file with the grammar details. The following is a very basic example:

```
{
    "Rules": [
        {
            "Type": 0,
            "Key": "Launch calculator rule",
            "Data": [
                {
                    "Type": 1,
                    "Key": null,
                    "Data": "Hey computer, launch the calculator."
                }
            ]
        }
    ],
    "Replacements": {},
    "Prefix": "Hey computer"
}
```

Rules is an array of different things for speech recognition to listen for. Because VOSK is unable to work with non-standard words, replacements can be used to have VOSK listen for particular words and replace them with non-standard and fantasy words. If all of the phrases it listens for start with the same words, you can use prefix to make sure the first word(s) match the prefix before trying to determine the rest of the spoken phrase.

Once you have the grammar JSON file written, you can send the initialize speech recognition request.

```
{
    "start_speech_recognition": {
        "vosk_model": "vosk-model-small-en-us-0.15",
        "grammar_file": "/tmp/grammar.json",
        "required_confidence": 80
    }
}
```

The VOSK model is a name of the [VOSK model](https://alphacephei.com/vosk/models) to use. By default if not provided, the small English US model will be used. Grammar file is the path to the generated grammar JSON file, and required confidence is the percent confidence that the phrase matches what the user said. Note that VOSK does not return a confidence in what it hears, so this is just the confidence that what VOSK thinks you said matches one of the phrases in the grammar file.

### Stop Speech Recognition

If you want speech recognition to be stopped, then you can send the following request. Note that if you want to restart speech recognition, you will need to send another start_speech_recognition request.

```
{
    "stop_speech_recognition": {}
}
```

### Ping 

In order to make sure that the PySpeechService application isn't running in the background indefinitely for no reason, it will shut down if it hasn't received any requests in 5 minutes. To avoid this, it is recommended to send the ping request every 60 seconds.

```
{
    "ping": {
        "time": "2024-03-14 21:34:06
    }
}
```

### Shutdown PySpeechService

In many cases, the PySpeechService application shouldn't need to be manually shutdown. If it doesn't receive a message, it will automatically shutdown after around 5 minutes. If the connected application disconnects, it will also shutdown. However, if for any reason your application needs to shutdown the application and wants to do so gracefully, it can call the shutdown request.

```
{
    "shutdown": {}
}
```

## Step 4: Receive PySpeechService Responses

PySpeechService will send various responses to the gRPC stream when certain events occur.

### Speech Settings Set

This is returned when you initialize the default speech settings.

```
{
    "speech_settings_set": {
        "successful": true
    }
}
```

### Speech Update Response

You will receive updates based on PySpeechService's progress on speech requests. These will happen when PySpeechService starts or ends a line.

```
{
    "speak_update": {
        "message": "This is a new message.\nThis is the second line.",
        "chunk": "This is the second line.",
        "is_start_of_message": false,
        "is_start_of_chunk": true,
        "is_end_of_message": false,
        "is_end_of_chunk": false,
        "has_another_request": false
    }
}
```

The message is the full text of the original request, while the chunk is the current part of the message that is being spoken. To make responses faster, paragraphs and multiline messages are broken out into smaller chunks. The four boolean values notify you of the current status of the TTS request, while the has_another_request value informs you of if there is another request pending in the queue.

### Set Volume Response

This is returned when you attempt to set the default volume used by text to speech.

```
{
    "set_volume": {
        "successful": true
    }
}
```

### Speech Recognition Initialized Response

This is returned when you attempt to start speech recognition.

```
{
    "speech_recognition_started": {
        "successful": true
    }
}
```

### Speech Recognition Response

Whenever PySpeechService is able to recognize text, it send a response with various details about the text that it recognizes.

```
{
    "speech_recognized": {
        "heard_text": "hey computer lunch eh calculator",
        "recognized_text": "hey computer launch the calculator",
        "recognized_rule": "Launch calculator rule",
        "confidence": 83,
        "semantics": []
    }
}
```

Heard text is the text recognized by the VOSK speech recognition, whereas the recognized text is the matched passed in phrase that PySpeechService thinks that heard text matches with. The confidence is the confidence that the heard text matches the recognized text. The recognized rule is the rule matching the recognized text, and semantics are the matched key value pairs in the recognized text.

### Ping

Each time you send a ping request, you'll get a ping response. This way you can confirm you're also receiving responses.

```
{
    "ping": {
        "time": "2025-03-15 05:12:32.137872"
    }
}
```

### Error

When the PySpeechService runs into an error, it will send a message that can be used for troubleshooting.

```
{
    "error": {
        "error_message": "Speech settings have not been initialized. Call set_speech_settings first.",
        "exception": ""
    }
}
```

Note that these error messages and exception details are not useful messages to display to the user, but are meant for developer messaging.