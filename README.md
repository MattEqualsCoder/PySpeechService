# PySpeechService

PySpeechService is a cross platform Python application that can be communicted with via gRPC for text-to-speech and speech recognition. This can be used by applications in languages and environments that may otherwise be hard to accomplish text-to-speech and speech recognition. For example, this was developed so that the SMZ3 Cas' Randomizer C# application could have text-to-speech and speech recognition on Linux.

## Installation

### Prerequisites

* Python 3.12
* PortAudio
    Ubuntu/Debian: apt-get install libportaudio2
    RHEL: dnf install portaudio
    Arch: pacman -S portaudio
* ffmpeg

### Installation Methods

#### Installation Method 1: Executable Binary

* Download the latest binary from the [releases page](https://github.com/MattEqualsCoder/PySpeechService/releases)
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

### Build

First, run poetry install to install requirements.

Next, to release run poetry build and poetry publish to build and publish.