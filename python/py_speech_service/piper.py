import os
import subprocess
from pathlib import Path
from typing import Optional
import yapper.constants as c
import yapper.utils
from platformdirs import user_data_dir
from pydub import AudioSegment
from yapper import PiperSpeaker, PiperVoiceUS, PiperVoiceUK
from yapper.utils import (
    APP_DIR,
    PLATFORM,
    get_random_name,
    install_piper, download_piper_model
)

class Piper(PiperSpeaker):

    voice_onnx_files: dict[PiperVoiceUS | PiperVoiceUK, str] = {}
    voice_conf_files: dict[PiperVoiceUS | PiperVoiceUK, str] = {}

    def __init__(self, onnx_path: Optional[str] = None, conf_path: Optional[str] = None, piper_voice: str = "", alt_piper_voice: str = ""):
        yapper.utils.APP_DIR = Path(user_data_dir("py_speech_service"))
        if onnx_path and conf_path:
            install_piper(False)
            self.exe_path = str(
                APP_DIR
                / "piper"
                / ("piper.exe" if PLATFORM == c.PLATFORM_WINDOWS else "piper")
            )
            self.onnx_f = onnx_path
            self.conf_f = conf_path
        else:
            voice = self.string_to_voice(piper_voice)
            super().__init__(voice)
            self.voice_onnx_files[voice] = self.onnx_f
            self.voice_conf_files[voice] = self.conf_f
            if alt_piper_voice and alt_piper_voice != piper_voice:
                voice = self.string_to_voice(alt_piper_voice)
                quality = PiperSpeaker.VOICE_QUALITY_MAP[voice]
                onnx_f, conf_f = download_piper_model(
                    voice, quality, False
                )
                onnx_f, conf_f = str(onnx_f), str(conf_f)
                self.voice_onnx_files[voice] = onnx_f
                self.voice_conf_files[voice] = conf_f

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
                    voice, quality, False
                )
                self.onnx_f, self.conf_f = str(self.onnx_f), str(self.conf_f)
                self.voice_onnx_files[voice] = self.onnx_f
                self.voice_conf_files[voice] = self.conf_f

    def text_to_wav(self, text: str, file: str, rate: float = 1):
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

    def text_to_pydub(self, text: str, rate: float = 1) -> Optional[AudioSegment]:
        f = APP_DIR / f"{get_random_name()}.wav"
        try:
            self.text_to_wav(text, str(f), rate)
            audio_segment = AudioSegment.from_file(f, format="wav")
        finally:
            if f.exists():
                os.remove(f)
        return audio_segment