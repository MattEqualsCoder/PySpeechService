import asyncio
import json
import logging
import os
import sys
import time
import traceback
from logging.handlers import RotatingFileHandler
from pathlib import Path

from platformdirs import user_data_dir

from py_speech_service.grpc_server import GrpcServer
from py_speech_service.speaker import Speaker, SpeechSettings
from py_speech_service.speech_recognition import SpeechRecognition
from py_speech_service.version import Version


def cli():
    try:

        log_path = get_arg_value("-l")
        if not log_path:
            log_path = os.path.join(user_data_dir("py_speech_service"), "log.log")

        Path(os.path.dirname(log_path)).mkdir(parents=True, exist_ok=True)

        logging.basicConfig(
            handlers=[RotatingFileHandler(log_path, maxBytes=500000, backupCount=3)],
            level=logging.DEBUG if get_arg_flag("-d") else logging.INFO,
            format="[%(asctime)s] %(levelname)s [%(name)s.%(funcName)s:%(lineno)d] %(message)s",
            datefmt='%Y-%m-%d %H:%M:%S')

        logging.info("Starting py-speech-service v" + Version.name())

        arg_array = sys.argv

        first_arg = arg_array[0]
        second_arg = arg_array[1] if len(arg_array) > 1 else 0

        if first_arg == "version" or second_arg == "version" or first_arg == "-v" or second_arg == "-v" or first_arg == "--version" or second_arg == "--version":
            logging.info("Returning version")
            print("py-speech-service v" + Version.name())
        elif first_arg == "speak" or second_arg == "speak":
            logging.info("Starting speak mode")
            speech = arg_array.pop()
            speaker = Speaker()
            speaker.init_speech_settings(SpeechSettings())
            asyncio.run(speaker.speak_basic_line(speech))

        elif first_arg == "recognition" or second_arg == "recognition":
            logging.info("Starting speech recognition mode")

            grammar = ""
            model = ""
            for arg in arg_array:
                if arg.startswith("-g="):
                    grammar = get_single_arg(arg, "-g")
                elif arg.startswith("-m"):
                    model = get_single_arg(arg, "-m")


            if not grammar:
                print("Listening for 'test speech recognition'")
                write_dummy_speech_json()
                grammar = '/tmp/py_tst_data.json'

            logging.info("Grammar file: " + grammar)
            logging.info("model: " + model)

            speech_recognition = SpeechRecognition()
            speech_recognition.set_speech_recognition_details(grammar, model)
            asyncio.run(speech_recognition.start_speech_recognition(None))

        elif first_arg == "test" or second_arg == "test":

            print("Starting Test")

            print("Step 1: Attempting to say \"This is a test message\"")
            speaker = Speaker()
            speaker.init_speech_settings(SpeechSettings())
            asyncio.run(speaker.speak_basic_line("This is a test message"))

            print("Step 2: Waiting for \"test speech recognition\" to be said")
            write_dummy_speech_json()
            speech_recognition = SpeechRecognition()
            speech_recognition.stop_after_first_recognition = True
            speech_recognition.set_speech_recognition_details('/tmp/py_tst_data.json', "")
            asyncio.run(speech_recognition.start_speech_recognition(None))

        elif first_arg == "service" or second_arg == "service":
            logging.info("Starting gRPC server mode")
            server = GrpcServer()
            asyncio.run(server.start())
        else:
            logging.info("Printing documentation")
            print("py-speech-service v" + Version.name())
            print("Usage: py-speech-service (speak/recognition/service)")
            print("  py-speech-service speak \"text to speech\"")
            print("  py-speech-service recognition -g \"path to grammar file\" -m \"path to VOSK model folder\"")
            print("  py-speech-service test")
            print("  py-speech-service service -g \"path to grammar file\" -m \"path to VOSK model folder\" -p \"preferred port\"")

    except Exception as e:
        logging.error(e)
        logging.error(traceback.format_exc())

    time.sleep(1)

def get_arg_value(arg: str):
    for current_arg in sys.argv:
        if current_arg.startswith(arg + "="):
            return get_single_arg(current_arg, arg)
    return None

def get_arg_flag(arg: str):
    for current_arg in sys.argv:
        if current_arg == arg:
            return True
    return False

def get_single_arg(arg: str, command: str):
    if arg.startswith(command + "=\""):
        return arg[len(command)+2:-1]
    else:
        return arg[len(command)+1:]

def write_dummy_speech_json():
    json_data = {
        "Rules": [
            {
                "Type": 0,
                "Key": "Test Rule",
                "Data": [
                    {
                        "Type": 1,
                        "Key": None,
                        "Data": "Test speech recognition"
                    }
                ]
            }
        ],
        "Replacements": {},
        "Prefix": ""
    }

    # Write to a JSON file
    with open('/tmp/py_tst_data.json', 'w', encoding='utf-8') as f:
        json.dump(json_data, f, ensure_ascii=False, indent=4)

if __name__ == "__main__":
    cli()
