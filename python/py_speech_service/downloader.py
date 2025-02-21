import os
import tempfile
import requests
import zipfile
import tarfile

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