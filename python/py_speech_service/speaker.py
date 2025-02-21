import asyncio
import json
import logging
import re
import typing

import pyaudio
import numpy
from pydub.silence import detect_leading_silence
from pydub.utils import make_chunks

from py_speech_service import speech_service_pb2
from py_speech_service.piper import Piper

trim_leading_silence = lambda x: x[detect_leading_silence(x):]
trim_trailing_silence = lambda x: trim_leading_silence(x.reverse()).reverse()
strip_silence = lambda x: trim_trailing_silence(trim_leading_silence(x))

class SpeechSettings:
    model_name: str = "hfc_female"
    onnx_path: str = ""
    config_path: str = ""
    alt_model_name: str = "hfc_male"
    alt_onnx_path: str = ""
    alt_config_path: str = ""
    speed: float = 1
    pitch: float = 1
    gain: float = 0
    
    def __init__(self, other = None):
        if type(other) is SpeechSettings:
            self.model_name = other.model_name
            self.onnx_path = other.onnx_path
            self.config_path = other.config_path
            self.alt_model_name = other.alt_model_name
            self.alt_onnx_path = other.alt_onnx_path
            self.alt_config_path = other.alt_config_path
            self.speed = other.speed
            self.pitch = other.pitch
            self.gain = other.gain
        elif other is not None:
            self.model_name = other.model_name if hasattr(other, "model_name") and other.model_name else "hfc_female"
            self.onnx_path = other.onnx_path if hasattr(other, "onnx_path") and other.onnx_path else ""
            self.config_path = other.config_path if hasattr(other, "config_path") and other.config_path else ""
            self.alt_model_name = other.alt_model_name if hasattr(other, "alt_model_name") and other.alt_model_name else "hfc_male"
            self.alt_onnx_path = other.alt_onnx_path if hasattr(other, "alt_onnx_path") and other.alt_onnx_path else ""
            self.alt_config_path = other.alt_config_path if hasattr(other, "alt_config_path") and other.alt_config_path else ""
            self.speed = numpy.clip(other.speed, 0.5, 2.0) if hasattr(other, "speed") and other.speed else 1
            self.gain = numpy.clip(other.gain, -100.0, 100.0) if hasattr(other, "gain") and other.gain else 0
            self.pitch = numpy.clip(other.pitch, 0.5, 1.5) if hasattr(other, "pitch") and other.pitch else 1


class PendingSpeechRequest:
    message: str
    silence_seconds: float
    speech_settings: SpeechSettings
    use_alt_voice: bool

    original_message: str
    first_request_of_message: bool
    last_request_of_message: bool

    def to_string(self):
        data = {
            "message": str(self.message) if hasattr(self, "message") else "",
            "silence_seconds": str(self.silence_seconds) if hasattr(self, "silence_seconds") else "",
            "model_name": str(self.speech_settings.model_name) if hasattr(self, "model_name") else "",
            "onnx_path": str(self.speech_settings.onnx_path) if hasattr(self, "onnx_path") else "",
            "config_path": str(self.speech_settings.config_path) if hasattr(self, "config_path") else "",
            "use_alt_voice": str(self.use_alt_voice) if hasattr(self, "use_alt_voice") else "",
            "speed_modifier": str(self.speech_settings.speed) if hasattr(self, "speech_settings") else "",
            "gain_modifier": str(self.speech_settings.gain) if hasattr(self, "speech_settings") else "",
            "pitch_modifier": str(self.speech_settings.pitch) if hasattr(self, "speech_settings") else "",
        }
        return json.dumps(data)
    


class Speaker:

    speech_queue: asyncio.Queue[PendingSpeechRequest] = asyncio.Queue()
    grpc_response_queue: typing.Optional[asyncio.Queue] = None
    shutdown_event = asyncio.Event()
    stop_talking_event = asyncio.Event()

    speech_settings: SpeechSettings = SpeechSettings()
    piper: typing.Optional[Piper] = None
    is_done = False

    def start(self):
        asyncio.create_task(self.process_speech_queue())

    async def speak_basic_line(self, line: str):
        asyncio.create_task(self.process_speech_queue())
        response_queue = asyncio.Queue()
        self.set_grpc_response_queue(response_queue)
        await self.speak(line)
        while not self.shutdown_event.is_set():  # Keep running unless shutdown is triggered
            try:
                response = await asyncio.wait_for(response_queue.get(), timeout=1.0)  # Wait for 1 second
                if hasattr(response.speak_update, "is_end_of_message") and response.speak_update.is_end_of_message:
                    break
            except asyncio.TimeoutError:
                continue  # Retry checking

    def init_speech_settings(self, settings: SpeechSettings):
        if self.piper is None:
            self.piper = Piper()
        self.piper.set_speech_settings(settings.alt_onnx_path, settings.alt_config_path, settings.alt_model_name)
        self.piper.set_speech_settings(settings.onnx_path, settings.config_path, settings.model_name)
        self.speech_settings = settings
        response = speech_service_pb2.SpeechServiceResponse()
        response.speech_settings_set.successful = True
        asyncio.create_task(self.grpc_response_queue.put(response))

    async def process_speech_queue(self):
        logging.info("Started processing speech queue")
        print("Started processing speech queue")
        while not self.shutdown_event.is_set():  # Keep running unless shutdown is triggered
            try:
                item = await asyncio.wait_for(self.speech_queue.get(), timeout=1.0)  # Wait for 1 second
                await self.__handle_request(item)
            except asyncio.TimeoutError:
                continue  # Retry checking
        logging.info("Stopped processing speech queue")
        print("Stopped processing speech queue")
        while not self.speech_queue.empty():
            self.speech_queue.get_nowait()
            self.speech_queue.task_done()

    def set_grpc_response_queue(self, queue: asyncio.Queue):
        self.grpc_response_queue = queue

    async def speak(self, message: str, settings: typing.Optional[SpeechSettings] = None):

        request = PendingSpeechRequest()

        if settings is None:
            request.speech_settings = SpeechSettings(self.speech_settings)
        else:
            request.speech_settings = SpeechSettings(settings)

        if not message.__contains__("</") and not message.__contains__("/>"):
            request.message = message
            request.original_message = message
            request.first_request_of_message = True
            request.last_request_of_message = True
            await self.speech_queue.put(request)
        else:
            parts = self.__split_by_tags(message)
            requests: list[PendingSpeechRequest] = []
            tags: list[str] = []

            for part in parts:
                if part == '':
                    continue
                if part.startswith("<break"):
                    requests.append(self.__create_ssml_request(request, part, tags))
                elif part.startswith("</"):
                    tags.pop()
                elif part.startswith("<"):
                    tags.append(part)
                else:
                    requests.append(self.__create_ssml_request(request, part, tags))

            is_first: bool = True
            last_request = requests[len(requests)-1]
            for request in requests:
                request.first_request_of_message = is_first
                request.last_request_of_message = request == last_request
                request.original_message = message
                is_first = False
                await self.speech_queue.put(request)

    def stop_speaking(self):
        try:
            while not self.speech_queue.empty():
                self.speech_queue.get_nowait()
                self.speech_queue.task_done()
        except Exception as e:
            logging.info("Cleared speech queue")
            print("Cleared speech queue")

        self.stop_talking_event.set()

    def shutdown(self):
        self.shutdown_event.set()

    @staticmethod
    def __split_by_tags(html: str) -> list[str]:
        pattern = r"(</?[^>]+>)|([^<]+)"
        matches = re.findall(pattern, html)
        result = [match[0].strip() or match[1].strip() for match in matches if match[0] or match[1]]
        return result

    @staticmethod
    def __extract_attributes(tag) -> dict[str, str]:
        pattern = r'(\w+)=["\']([^"\']*?)["\']'
        attributes = dict(re.findall(pattern, tag))
        return attributes

    def __create_ssml_request(self, initial_request: PendingSpeechRequest, message: str, prosody_tags: list[str]) -> PendingSpeechRequest:
        request = PendingSpeechRequest()
        request.speech_settings = SpeechSettings(initial_request.speech_settings)

        if message.startswith("<break"):
            attr = self.__extract_attributes(message)
            if attr.__contains__("time"):
                if attr["time"].endswith("ms"):
                    request.silence_seconds = float(attr["time"].replace("ms", "")) / 1000
                elif attr["time"].endswith("s"):
                    request.silence_seconds = float(attr["time"].replace("s", ""))
                else:
                    request.silence_seconds = 1
            elif attr.__contains__("strength"):
                if attr["strength"] == "small":
                    request.silence_seconds = .25
                elif attr["strength"] == "large":
                    request.silence_seconds = 2.5
                else:
                    request.silence_seconds = 1
            else:
                request.silence_seconds = 1

        else:
            for tag in prosody_tags:
                if tag.startswith("<prosody"):
                    attr = self.__extract_attributes(tag)
                    if attr.__contains__("volume"):
                        self.__apply_gain(request, attr["volume"])
                    if attr.__contains__("rate"):
                        self.__apply_rate(request, attr["rate"])
                    if attr.__contains__("pitch"):
                        self.__apply_pitch(request, attr["pitch"])
                elif tag.startswith("<voice"):
                    request.use_alt_voice = True

            request.message = message
        return request

    @staticmethod
    def __apply_gain(request: PendingSpeechRequest, volume: str):
        if volume.isnumeric():
            request.speech_settings.gain = float(volume) / 100.0 * 16 + -16
        elif volume == "silent":
            request.speech_settings.gain = -50
        elif volume == "x-soft":
            request.speech_settings.gain = -16
        elif volume == "soft":
            request.speech_settings.gain = -12
        elif volume == "medium":
            request.speech_settings.gain = -8
        elif volume == "loud":
            request.speech_settings.gain = -4
        else:
            request.speech_settings.gain = 0

    @staticmethod
    def __apply_rate(request: PendingSpeechRequest, rate: str):
        if rate.isnumeric():
            rate = float(rate)
            if rate < .5:
                request.speech_settings.speed = 0.5
            elif rate > 2:
                request.speech_settings.speed = 2
            else:
                request.speech_settings.speed = rate
        elif rate == "x-slow":
            request.speech_settings.speed = .5
        elif rate == "slow":
            request.speech_settings.speed = .8
        elif rate == "fast":
            request.speech_settings.speed = 1.55
        elif rate == "x-fast":
            request.speech_settings.speed *= 2

    @staticmethod
    def __apply_pitch(request: PendingSpeechRequest, pitch: str):
        if pitch.__contains__("%"):
            pitch = float(pitch.replace("%", "")) / 100.0
            if pitch < 0.5:
                request.speech_settings.pitch = 0.5
                request.speech_settings.speed *= 1.5
            elif pitch > 1.5:
                request.speech_settings.pitch = 1.5
                request.speech_settings.speed *= .5
            else:
                request.speech_settings.pitch = pitch
                request.speech_settings.speed *= 2 - pitch
        elif pitch == "x-low":
            request.speech_settings.pitch = 0.9
            request.speech_settings.speed *= 1.1
        elif pitch == "low":
            request.speech_settings.pitch = 0.95
            request.speech_settings.speed *= 1.05
        elif pitch == "high":
            request.speech_settings.pitch = 1.05
            request.speech_settings.speed *= 0.95
        elif pitch == "x-high":
            request.speech_settings.pitch = 1.1
            request.speech_settings.speed *= .9

    async def __send_response(self, request: PendingSpeechRequest, is_start: bool):
        if self.grpc_response_queue:
            response = speech_service_pb2.SpeechServiceResponse()
            response.speak_update.message = request.original_message
            response.speak_update.chunk = request.message
            response.speak_update.is_start_of_message = is_start and request.first_request_of_message
            response.speak_update.is_start_of_chunk = is_start
            response.speak_update.is_end_of_message = not is_start and (request.last_request_of_message or self.stop_talking_event.is_set())
            response.speak_update.is_end_of_chunk = not is_start
            await self.grpc_response_queue.put(response)

    async def __handle_request(self, request: PendingSpeechRequest):
        self.is_speaking = True

        if hasattr(request, "message") and request.message:

            await self.__send_response(request, True)

            speech_settings = self.speech_settings
            if hasattr(request, "speech_settings"):
                speech_settings = request.speech_settings

            if hasattr(request, "use_alt_voice") and request.use_alt_voice:
                self.piper.set_speech_settings(speech_settings.alt_onnx_path, speech_settings.alt_config_path,
                                               speech_settings.alt_model_name)
            else:
                self.piper.set_speech_settings(speech_settings.onnx_path, speech_settings.config_path,
                                               speech_settings.model_name)

            sound = self.piper.text_to_pydub(request.message, speech_settings.speed)
            frame_rate_modifier = 1

            if speech_settings.gain != 0:
                sound = sound.apply_gain(speech_settings.gain)
            if speech_settings.pitch != 1:
                frame_rate_modifier = speech_settings.pitch

            new_frame_rate = int(sound.frame_rate * frame_rate_modifier)
            sound = sound._spawn(sound.raw_data, overrides={"frame_rate": new_frame_rate})

            p = pyaudio.PyAudio()
            stream = p.open(format=p.get_format_from_width(sound.sample_width),
                            channels=sound.channels,
                            rate=sound.frame_rate,
                            output=True)

            logging.info("Saying: " + str(request.message))

            try:
                for chunk in make_chunks(sound, 500):
                    if not self.stop_talking_event.is_set() and not self.shutdown_event.is_set():
                        await asyncio.to_thread(stream.write, chunk._data)

            finally:
                await self.__send_response(request, False)
                stream.stop_stream()
                stream.close()
                p.terminate()
                self.stop_talking_event.clear()
                self.is_speaking = False
        elif hasattr(request, "silence_seconds") and request.silence_seconds:
            await asyncio.sleep(request.silence_seconds)
            self.is_speaking = False