import logging
import subprocess
from pathlib import Path
from typing import Optional

import yapper.constants as c
from platformdirs import user_data_dir
from yapper import PiperSpeaker, PiperVoiceUS, PiperVoiceUK
from yapper.utils import (
    PLATFORM,
)

from py_speech_service.downloader import download_piper, download_piper_model


class Piper(PiperSpeaker):

    voice_onnx_files: dict[PiperVoiceUS | PiperVoiceUK, str] = {}
    voice_conf_files: dict[PiperVoiceUS | PiperVoiceUK, str] = {}
    piper_setup: bool = False
    conf_setup: bool = False

    def __init__(self, onnx_path: Optional[str] = None, conf_path: Optional[str] = None, piper_voice: str = "", alt_piper_voice: str = ""):
        app_dir = Path(user_data_dir("py_speech_service"))

        try:
            download_piper(app_dir)
        except Exception as e:
            print("Download piper error")
            logging.error("Download piper error")
            logging.error(e)
            return

        self.exe_path = str(
            app_dir
            / "piper"
            / ("piper.exe" if PLATFORM == c.PLATFORM_WINDOWS else "piper")
        )

        if onnx_path and conf_path:
            self.onnx_f = onnx_path
            self.conf_f = conf_path
            self.conf_setup = Path(onnx_path).exists() and Path(conf_path).exists()
        else:
            voice = self.string_to_voice(piper_voice)
            quality = PiperSpeaker.VOICE_QUALITY_MAP[voice]
            onnx_f, conf_f = download_piper_model(
                voice, quality, app_dir
            )
            self.onnx_f, self.conf_f = str(onnx_f), str(conf_f)
            self.voice_onnx_files[voice] = self.onnx_f
            self.voice_conf_files[voice] = self.conf_f

            if alt_piper_voice and alt_piper_voice != piper_voice:
                voice = self.string_to_voice(alt_piper_voice)
                quality = PiperSpeaker.VOICE_QUALITY_MAP[voice]
                onnx_f, conf_f = download_piper_model(
                    voice, quality, app_dir
                )
                onnx_f, conf_f = str(onnx_f), str(conf_f)
                self.voice_onnx_files[voice] = onnx_f
                self.voice_conf_files[voice] = conf_f

            if self.onnx_f and self.conf_f:
                self.conf_setup = True

        self.piper_setup = True

    def is_valid(self):
        return self.piper_setup and self.conf_setup

    @staticmethod
    def string_to_voice(voice: str) -> PiperVoiceUS | PiperVoiceUK:
        try:
            return PiperVoiceUS(voice)
        except ValueError:
            try:
                return PiperVoiceUK(voice)
            except ValueError:
                return PiperVoiceUS.HFC_FEMALE

    def set_speech_settings(self, onnx_path: Optional[str] = None, conf_path: Optional[str] = None, piper_voice: str = ""):
        app_dir = Path(user_data_dir("py_speech_service"))
        if onnx_path and conf_path:
            self.onnx_f = onnx_path
            self.conf_f = conf_path
        else:
            voice = self.string_to_voice(piper_voice)
            if self.voice_onnx_files.__contains__(voice):
                self.onnx_f = self.voice_onnx_files[voice]
                self.conf_f = self.voice_conf_files[voice]
            else:
                quality = PiperSpeaker.VOICE_QUALITY_MAP[voice]
                self.onnx_f, self.conf_f = download_piper_model(
                    voice, quality, app_dir
                )
                self.onnx_f, self.conf_f = str(self.onnx_f), str(self.conf_f)
                self.voice_onnx_files[voice] = self.onnx_f
                self.voice_conf_files[voice] = self.conf_f

    def text_to_wav(self, text: str, file: str, rate: float = 1) -> bool:
        if not self.piper_setup:
            print("Piper not setup")
            logging.error("Piper not setup")
            return False
        length_scale = 1 / rate
        subprocess.run(
            [
                self.exe_path,
                "-m",
                self.onnx_f,
                "-c",
                self.conf_f,
                "-f",
                file,
                "-q",
                "--length_scale",
                str(length_scale)
            ],
            input=text.encode("utf-8"),
            stdout=subprocess.DEVNULL,
            check=True
        )
        return True
