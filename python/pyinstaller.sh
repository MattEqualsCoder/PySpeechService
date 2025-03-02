#!/bin/bash
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
rm -rf $SCRIPT_DIR/build/*
cd $SCRIPT_DIR/py_speech_service/
pyinstaller --noconsole --onefile --collect-all vosk --collect-all grpc --hidden-import pyaudio __main__.py
mv $SCRIPT_DIR/py_speech_service/dist $SCRIPT_DIR/build/dist
mv $SCRIPT_DIR/py_speech_service/build $SCRIPT_DIR/build/build
mv $SCRIPT_DIR/build/dist/__main__ $SCRIPT_DIR/build/dist/py-speech-service
rm $SCRIPT_DIR/py_speech_service/__main__.spec