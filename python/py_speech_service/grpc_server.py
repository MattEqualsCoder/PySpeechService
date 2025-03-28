import asyncio
import datetime
import json
import logging
import sys
import time
import traceback
from asyncio import Server, Queue

from grpc import aio

from py_speech_service import speech_service_pb2_grpc, speech_service_pb2
from py_speech_service.speaker import Speaker, SpeechSettings
from py_speech_service.speech_recognition import SpeechRecognition
from py_speech_service.version import Version


class GrpcServer:

    speaker: Speaker
    speech_recognition: SpeechRecognition
    server: Server
    shutdown_event = asyncio.Event()
    response_queue = Queue()
    speech_initialized = False
    last_message = time.time()

    def __init__(self):
        self.speaker = Speaker()
        self.speech_recognition = SpeechRecognition()

    async def start(self):
        server = aio.server()
        self.server = server
        speech_service_pb2_grpc.add_SpeechServiceServicer_to_server(self, server)
        port = server.add_insecure_port("[::]:0")
        await server.start()
        logging.info("Listening to gRPC connections on port " + str(port))

        print(json.dumps({
            "version": Version.name(),
            "port": port
        }))

        asyncio.create_task(self.monitor())
        sys.stdout.flush()
        self.speaker.start()
        await self.shutdown_event.wait()
        self.speaker.shutdown()
        if self.speech_recognition:
            self.speech_recognition.shutdown()
        await server.stop(5)
        time.sleep(1)

    async def StartSpeechService(self, request_iterator, context):
        self.speaker.set_grpc_response_queue(self.response_queue)
        self.speech_recognition.set_grpc_response_queue(self.response_queue)
        asyncio.create_task(self.process_queue(context))
        self.last_message = time.time()

        async for request in request_iterator:
            try:
                self.last_message = time.time()

                logging.debug(str(request))

                if request.HasField("start_speech_recognition"):
                    logging.info("Received gRPC start_speech_recognition request")
                    print("Received gRPC start_speech_recognition request")

                    vosk_model = request.start_speech_recognition.vosk_model if hasattr(request.start_speech_recognition, "vosk_model") else None
                    grammar_file = request.start_speech_recognition.grammar_file if hasattr(request.start_speech_recognition, "grammar_file") else None
                    required_confidence = request.start_speech_recognition.required_confidence if hasattr(request.start_speech_recognition, "required_confidence") else 80

                    successful = self.speech_recognition.set_speech_recognition_details(grammar_file, vosk_model, required_confidence)
                    if successful:
                        asyncio.create_task(self.speech_recognition.start_speech_recognition(context))
                    else:
                        response = speech_service_pb2.SpeechServiceResponse()
                        response.speech_recognition_started.successful = False
                        await self.response_queue.put(response)

                elif request.HasField("set_speech_settings"):
                    logging.info(
                        "Received gRPC set_speech_settings request: " + str(request.set_speech_settings))
                    print("Received gRPC set_speech_settings request")

                    try:
                        self.speech_initialized = self.speaker.init_speech_settings(SpeechSettings(request.set_speech_settings.speech_settings))
                    except Exception as e:
                        response = speech_service_pb2.SpeechServiceResponse()
                        response.speech_settings_set.successful = True
                        await self.response_queue.put(response)
                        logging.error("Error initializing speech settings: " + repr(e))
                        logging.error(traceback.format_exc())
                        response = speech_service_pb2.SpeechServiceResponse()
                        response.error.error_message = "Error initializing speech settings"
                        response.error.exception = repr(e)
                        await self.response_queue.put(response)

                elif request.HasField("speak"):
                    logging.info(
                        "Received gRPC speak request: \"" + ' '.join(request.speak.message.splitlines()) + "\"")
                    print("Received gRPC speak request")

                    if self.speech_initialized:
                        speech_settings = SpeechSettings(request.speak.speech_settings) if request.speak.HasField("speech_settings") else None
                        await self.speaker.speak(request.speak.message, speech_settings, request.speak.message_id)
                    else:
                        response = speech_service_pb2.SpeechServiceResponse()
                        response.error.error_message = "Speech settings have not been initialized. Call set_speech_settings first."
                        await self.response_queue.put(response)

                elif request.HasField("stop_speaking"):
                    logging.info(
                        "Received gRPC stop_speaking request")
                    print("Received gRPC stop_speaking request")

                    self.speaker.stop_speaking()
                elif request.HasField("shutdown"):
                    logging.info(
                        "Received gRPC shutdown request")
                    print("Received gRPC shutdown request")
                    break
                elif request.HasField("ping"):
                    print("Received gRPC ping")
                    logging.info(
                        "Received gRPC ping")
                    response = speech_service_pb2.SpeechServiceResponse()
                    response.ping.time = str(datetime.datetime.now())
                    await self.response_queue.put(response)
                elif request.HasField("stop_speech_recognition"):
                    print("Received stop speech recognition request")
                    self.speech_recognition.stop_speech_recognition()
                elif request.HasField("set_volume"):
                    logging.info("Received set volume request")
                    self.speaker.set_volume(request.set_volume.volume)
                    response = speech_service_pb2.SpeechServiceResponse()
                    response.set_volume.successful = True
                    await self.response_queue.put(response)

            except Exception as e:
                logging.error("Exception with speech service: " + str(e))
                logging.error(repr(e))
                logging.error(traceback.format_exc())
                response = speech_service_pb2.SpeechServiceResponse()
                response.error.error_message = "Exception with speech service: " + str(e)
                response.error.exception = repr(e)
                await self.response_queue.put(response)

        self.shutdown_event.set()

    async def process_queue(self, context):
        while not self.shutdown_event.is_set():
            response = await self.response_queue.get()
            await context.write(response)

    async def monitor(self):
        logging.info("Starting monitor")
        while time.time() - self.last_message < 300 and not self.shutdown_event.is_set():
            await asyncio.sleep(60)

        if not self.shutdown_event.is_set():
            logging.info("No message received in 5 minutes. Exiting.")
            print("No message received in 5 minutes. Exiting.")
            self.shutdown_event.set()


