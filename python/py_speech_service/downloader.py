import logging
import os
import ssl
import sys
import tempfile
import urllib
from pathlib import Path
from urllib.request import urlretrieve

import certifi
import yapper.constants as c
import requests
import zipfile
import tarfile
import platform

from yapper import PiperVoiceUS, PiperQuality, PiperVoiceUK

if os.name == "nt":
    PLATFORM = c.PLATFORM_WINDOWS
elif os.name == "posix":
    home = Path.home()
    if os.uname().sysname == "Darwin":
        PLATFORM = c.PLATFORM_MAC
    else:
        PLATFORM = c.PLATFORM_LINUX
else:
    print("your system is not supported")
    sys.exit()

def download_file(url: str) -> str:
    # Create a temporary file
    temp_file = tempfile.NamedTemporaryFile(delete=False)
    try:
        print(f"Downloading {url} ...")
        # Stream the download to avoid loading it into memory all at once
        response = requests.get(url, stream=True)
        response.raise_for_status()

        # Write the content to the temp file
        with open(temp_file.name, 'wb') as f:
            for chunk in response.iter_content(chunk_size=8192):
                f.write(chunk)

        print(f"Downloaded to {temp_file.name}")
        return temp_file.name
    except Exception as e:
        print(f"Error downloading file: {e}")
        raise


def extract_zip(source_file, dest_folder):
    print(f"Extracting {source_file} to {dest_folder} ...")
    # Create the destination folder if it doesn't exist
    os.makedirs(dest_folder, exist_ok=True)

    # Extract the ZIP file
    with zipfile.ZipFile(source_file, 'r') as zip_ref:
        zip_ref.extractall(dest_folder)

    print(f"Extraction complete. Files are in {dest_folder}")


def extract_tar_gz(source_file, dest_folder):
    print(f"Extracting TAR.GZ: {source_file} to {dest_folder} ...")
    # Create the destination folder if it doesn't exist
    os.makedirs(dest_folder, exist_ok=True)

    # Extract the TAR.GZ file
    with tarfile.open(source_file, 'r:gz') as tar_ref:
        tar_ref.extractall(dest_folder)

    print(f"TAR.GZ extraction complete. Files are in {dest_folder}")


def download_and_extract(url: str, destination_folder: str):
    downloaded_file: str = ""
    try:
        downloaded_file = download_file(url)
        if url.lower().endswith(".zip"):
            extract_zip(downloaded_file, destination_folder)
        elif url.lower().endswith(".tar.gz"):
            extract_tar_gz(downloaded_file, destination_folder)
        else:
            raise ValueError("Unsupported file type. Only .zip and .tar.gz are supported.")
    finally:
        if downloaded_file and os.path.exists(downloaded_file):
            os.remove(downloaded_file)
            print(f"Temporary file {downloaded_file} deleted.")

def get_json_data(url):
    try:
        print(f"Fetching JSON data from {url} ...")
        response = requests.get(url)
        response.raise_for_status()
        json_data = response.json()
        print("Data fetched successfully.")
        return json_data
    except requests.exceptions.RequestException as e:
        print(f"Error fetching data: {e}")
        raise

def download(url: str, file: str):
    logging.info(f"Downloading {url} to {file}")
    context = ssl.create_default_context(cafile=certifi.where())
    # urllib.request.urlretrieve(url, file, context=context)
    with urllib.request.urlopen(url, context=context) as response, open(file, 'wb') as out_file:
        out_file.write(response.read())
    logging.info(f"Download of {url} complete")

def download_piper(piper_dir: Path):

    """Installs piper into the app's home directory."""
    if (piper_dir / "piper").exists():
        return
    zip_path = piper_dir / "piper.zip"
    prefix = "https://github.com/rhasspy/piper/releases/download/2023.11.14-2"
    if PLATFORM == c.PLATFORM_LINUX:
        if platform.machine() in ("aarch64", "arm64"):
            nix_link = f"{prefix}/piper_linux_aarch64.tar.gz"
        elif platform.machine() in ("armv7l", "armv7"):
            nix_link = f"{prefix}/piper_linux_armv7l.tar.gz"
        else:
            nix_link = f"{prefix}/piper_linux_x86_64.tar.gz"
        download(nix_link, str(zip_path))
    elif PLATFORM == c.PLATFORM_WINDOWS:
        download(f"{prefix}/piper_windows_amd64.zip", str(zip_path))
    else:
        download(f"{prefix}/piper_macos_x64.tar.gz", str(zip_path))

    if PLATFORM == c.PLATFORM_WINDOWS:
        with zipfile.ZipFile(zip_path, "r") as z_f:
            z_f.extractall(piper_dir)
    else:
        with tarfile.open(zip_path, "r") as z_f:
            z_f.extractall(piper_dir)
    os.remove(zip_path)


def download_piper_model(
    voice: PiperVoiceUS | PiperVoiceUK,
    quality: PiperQuality,
    piper_dir: Path
) -> tuple[str, str]:
    voices_dir = piper_dir / "piper_voices"
    voices_dir.mkdir(exist_ok=True)
    lang_code = "en_US" if isinstance(voice, PiperVoiceUS) else "en_GB"
    voice, quality = voice.value, quality.value

    onnx_file = voices_dir / f"{lang_code}-{voice}-{quality}.onnx"
    conf_file = voices_dir / f"{lang_code}-{voice}-{quality}.onnx.json"

    prefix = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/"
    prefix += lang_code
    help_url = "https://huggingface.co/rhasspy/piper-voices/tree/main/en/"
    help_url += lang_code
    if not onnx_file.exists():
        try:
            onnx_url = (
                f"{prefix}/{voice}/{quality}/{onnx_file.name}?download=true"
            )
            download(onnx_url, str(onnx_file))
        except (KeyboardInterrupt, Exception) as e:
            onnx_file.unlink(missing_ok=True)
            if getattr(e, "status", None) == 404:
                raise Exception(
                    f"{voice}({quality}) is not available, please refer to"
                    f" {help_url} to check all available models"
                )
            raise e
    if not conf_file.exists():
        conf_url = f"{prefix}/{voice}/{quality}/{conf_file.name}?download=true"
        try:
            download(conf_url, str(conf_file))
        except (KeyboardInterrupt, Exception) as e:
            conf_file.unlink(missing_ok=True)
            raise e

    return str(onnx_file), str(conf_file)
