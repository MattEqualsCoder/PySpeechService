import asyncio
import json
import logging
import os
import queue
import traceback
from pathlib import Path
from typing import Optional

import sounddevice as sd
from platformdirs import user_data_dir
from vosk import Model, KaldiRecognizer, SetLogLevel

from py_speech_service import speech_service_pb2
from py_speech_service.downloader import get_json_data, download_and_extract
from py_speech_service.grammar_parser import GrammarParser


class SpeechRecognition:
    model: Model
    grammar_parser: GrammarParser
    required_confidence: float = 80
    stop_speech_recognition_event: Optional[asyncio.Event] = None
    continue_speech_recognition: bool = True
    grpc_response_queue: Optional[asyncio.Queue] = None
    shutdown_event = asyncio.Event()
    recognition_queue = asyncio.Queue()
    vosk_model_folder = os.path.join(user_data_dir("py_speech_service"), "vosk")
    stop_after_first_recognition: bool = False

    def __init__(self):
        SetLogLevel(-1)
        self.stop_speech_recognition_event = None

    def download_vosk_model(self, model_name: Optional[str]) -> str:
        model = model_name
        if not model:
            model = "vosk-model-small-en-us-0.15"
        if os.path.exists(self.get_vosk_model_path(model)):
            return self.get_vosk_model_path(model)
        url = self.get_vosk_download_url_by_name(model)
        download_and_extract(url, self.vosk_model_folder)
        return self.get_vosk_model_path(model)

    def get_vosk_model_path(self, model_name: Optional[str]) -> str:
        return os.path.join(self.vosk_model_folder, model_name)

    def get_vosk_download_url_by_name(self, vosk_model_name) -> str:
        try:
            json_data = get_json_data("https://alphacephei.com/vosk/models/model-list.json")
            for record in json_data:
                print(record.get('name') + " - " + record.get("obsolete") + " - " + record.get('url'))
                if record.get('name') == vosk_model_name and record.get('obsolete') == "false":
                    print('found!')
                    return record.get('url')
        except:
            return "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip"

    def set_speech_recognition_details(self, grammar_file: str, vosk_model: str, required_confidence: float = 80) -> bool:
        try:
            self.grammar_parser = GrammarParser()
            self.grammar_parser.set_grammar_file(grammar_file)
            try:
                os.remove(grammar_file)
            except:
                logging.error(f"Unable to delete {grammar_file}")
            model_path = ""

            if vosk_model is not None and vosk_model != "":
                if vosk_model.count("/") > 0 or vosk_model.count("\\") > 0:
                    logging.info("Setting VOSK model path as " + vosk_model)
                    model_path = vosk_model
                    self.model = Model(vosk_model)
                else:
                    logging.info("Downloading VOSK model " + vosk_model)
                    model_path = self.download_vosk_model(vosk_model)
                    logging.info("Setting VOSK model path as " + model_path)
                    self.model = Model(model_path)
            else:
                logging.info("Downloading default VOSK model vosk-model-small-en-us-0.15")
                model_path = self.download_vosk_model("vosk-model-small-en-us-0.15")
                logging.info("Setting VOSK model path as " + model_path)
                self.model = Model(model_path)

            self.required_confidence = required_confidence
            return Path(model_path).exists()
        except Exception as e:
            logging.error("Unable to start speech recognition: " + repr(e))
            print("Unable to start speech recognition", str(e))
            return False

    async def start_speech_recognition(self, context):
        asyncio.create_task(self.process_recognition_queue())
        await asyncio.to_thread(self.listen)
        await asyncio.sleep(1)

    async def process_recognition_queue(self):
        logging.info("Started processing speech recognition queue")
        print("Started processing speech recognition queue")
        while not self.shutdown_event.is_set():  # Keep running unless shutdown is triggered
            try:
                item = await asyncio.wait_for(self.recognition_queue.get(), timeout=1.0)  # Wait for 1 second
                await self.process_speech(item)
            except asyncio.TimeoutError:
                continue  # Retry checking
        self.recognition_queue.task_done()
        logging.info("Stopped processing speech recognition queue")
        print("Stopped processing speech recognition queue")

    def listen(self):
        if self.stop_speech_recognition_event:
            self.stop_speech_recognition_event.set()

        stop_speech_recognition_event = asyncio.Event()
        self.stop_speech_recognition_event = stop_speech_recognition_event

        device_info = sd.query_devices(sd.default.device[0], 'input')
        samplerate = int(device_info['default_samplerate'])
        speech_queue = queue.Queue()

        def record_callback(indata, frames, time, status):
            if status:
                logging.error(str(status))
            speech_queue.put(bytes(indata))

        words_json = json.dumps(self.grammar_parser.all_words)
        recognizer = KaldiRecognizer(self.model, samplerate, words_json)
        recognizer.SetWords(False)
        recognizer.SetGrammar(words_json)

        try:
            logging.info("Started listening to voice via VOSK")
            print("Started listening to voice via VOSK")
            with sd.RawInputStream(dtype='int16',
                                   channels=1,
                                   callback=record_callback):
                response = speech_service_pb2.SpeechServiceResponse()
                response.speech_recognition_started.successful = True
                if self.grpc_response_queue:
                    asyncio.run(self.grpc_response_queue.put(response))
                while not stop_speech_recognition_event.is_set() and not self.shutdown_event.is_set():
                    data = speech_queue.get()
                    if recognizer.AcceptWaveform(data):
                        recognized_text = recognizer.Result()
                        if recognized_text:
                            asyncio.run(self.recognition_queue.put(recognized_text))
            logging.info("Stopped listening to voice via VOSK")
            print("Stopped listening to voice via VOSK")
        except KeyboardInterrupt:
            print('Finished recording due to keyboard interrupt')
            logging.error("Finished recording due to keyboard interrupt")
        except Exception as e:
            print("Error from VOSK: " + str(e))
            logging.error("Error from VOSK: " + str(e))
            logging.error(e)
            logging.error(traceback.format_exc())

    async def process_speech(self, recognizer_result: str):
        try:
            result_dict = json.loads(recognizer_result)
            text_recognized = result_dict.get("text", "")
            if not text_recognized == "":

                match = self.grammar_parser.find_match(text_recognized, self.required_confidence)

                if match is not None:
                    logging.info("Matched text \"" + match.matched_text + "\" (heard \"" + text_recognized + "\"")
                    if self.grpc_response_queue:
                        response = speech_service_pb2.SpeechServiceResponse()
                        response.speech_recognized.heard_text = text_recognized
                        response.speech_recognized.recognized_text = match.matched_text
                        response.speech_recognized.recognized_rule = match.rule
                        response.speech_recognized.confidence = round(match.confidence, 2)
                        response.speech_recognized.semantics.update(match.values)
                        await self.grpc_response_queue.put(response)
                    else:
                        print("I heard: '" + text_recognized + "', but I am " + str(round(match.confidence, 2)) + "% sure you said '" + match.matched_text + "'")
                        if self.stop_after_first_recognition:
                            self.stop_speech_recognition()
                else:
                    logging.debug("Recognized text " + text_recognized)

        except Exception as e:
            print("Error processing speech: " + str(e))
            logging.error("Error processing speech")
            logging.error(e)
            logging.error(traceback.format_exc())

    def set_grpc_response_queue(self, queue: asyncio.Queue):
        self.grpc_response_queue = queue

    def stop_speech_recognition(self):
        if self.stop_speech_recognition_event is not None:
            self.stop_speech_recognition_event.set()

    def shutdown(self):
        self.shutdown_event.set()
