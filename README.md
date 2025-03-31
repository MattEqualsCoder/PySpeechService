# PySpeechService

PySpeechService is a cross platform Python application that can be communicted with via gRPC for text-to-speech and speech recognition. This can be used by applications in languages and environments that may otherwise be hard to accomplish text-to-speech and speech recognition. For example, this was developed so that the SMZ3 Cas' Randomizer C# application could have text-to-speech and speech recognition on Linux.

This repository houses both the python PySpeechService application as well as the [C# nuget package](https://www.nuget.org/packages/MattEqualsCoder.PySpeechService.Client#versions-body-tab) to use for generating grammar and communicating with the PySpeechService application.

Currently there are only builds for Linux with Windows and MacOS builds being planned for the future. The grammar generated is compatible with Microsoft's System.Speech libraries, however.

## Installation

### Prerequisites

* Python 3.12
* PortAudio
  - Ubuntu/Debian: apt-get install libportaudio2
  - RHEL: dnf install portaudio
  - Arch: pacman -S portaudio
* ffmpeg

### Installation Methods

There are multiple ways to install the PySpeechService application. Not that with some devies and setups, you may have issues with particular installations. It is recommended that you try them in the below order, and use the `py-speech-service test` command to verify that everything works.

Note that the first time you run, it will have to download Piper, VOSK, and models needed for text to speech and speech recognition.

#### Installation Method 1: Executable Binary

This is the easiest method (if it works).
* Download the latest binary from the [releases page](https://github.com/MattEqualsCoder/PySpeechService/releases). Note that due to python differences causing issues with detecting certain audio devices, there are two versions.
  * For users of Debian-based systems (Ubuntu, Linux Mint, etc.), download the "PySpeechService_Debian" version
  * For users of Arch or Fedora, download the "PySpeechService_RHEL_Arch" version
  * Use `py-speech-service test` to verify it works. If one works, try the other.
* Add the binary to a location that is in your path
  * Alternatively, you can add it to ~/.local/share/py_speech_service

#### Installation Method 2: pipx

pipx is recommended over pip as it installs the application into an isolated environment to avoid dependency conflicts with other Python applications, and it also creates it standalone application which can be ran directly via commandline.

First, you will want to install [pipx](https://pypa.github.io/pipx/).

```
$ pipx install py-speech-service

$ py-speech-service
```

#### Installation Method 3: pip

First, make sure you have pip installed and running: https://packaging.python.org/en/latest/tutorials/installing-packages/

```
$ pip install py-speech-service

$ python3 -m py-speech-service
```

### Testing

You can use the `py-speech-service test` or `python3 -m py-speech-service test` command to verify that everything is working. It'll say a quick line to test text to speech and will ask you to say "test speech recognition".

## Development

### Developer Documentation

For developers who are wanting to use the PySpeechService application for their own projects, there are three different READMEs to help.

* [PySpeechService application](https://github.com/MattEqualsCoder/PySpeechService/blob/main/python/README.md) - This documentation covers how to launch the PySpeechService application, connect to it via gRPC, and how what requests can be sent to the PySpeechService application via gRPC.
* [Grammar JSON](https://github.com/MattEqualsCoder/PySpeechService/blob/main/python/GRAMMAR_README.md) - Using speech recognition requires generating a JSON file with all of the rules and prompts to listen for. This documentation covers the structure of the JSON file.
* [C# Client Nuget Package](https://github.com/MattEqualsCoder/PySpeechService/blob/main/csharp/README.md) - For C# application, you can use this nuget package for generating grammar, launching the PySpeechService application, or sending/receiving requests and responses to the application.

### Build PySpeechService

First, run poetry install to install requirements.

Next, to release run poetry build and poetry publish to build and publish.

For the standalone application, run the `pyinstaller.sh` bash script.