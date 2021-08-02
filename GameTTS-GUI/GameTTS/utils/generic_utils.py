import json
import logging
from typing import Dict, List
from pathlib import Path
from pydub import AudioSegment
from scipy.io.wavfile import write
import numpy as np
from utils.config import APP_SETTINGS


def parse_json(json_str: str) -> str:
    try:
        json_obj = json.loads(json_str)
        return json_obj
    except Exception as e:
        logging.error(e)
        return False


def parse_csv(csv_file: str) -> List:
    with open(csv_file, "r", encoding="utf-8") as rf:
        for line in rf:
            print()


def prepare_for_queue(json_obj) -> Dict:
    for k, v in json_obj.items():
        print(k, v)

    return json_obj


def save_audio(file_name, audio_data):

    file_path = Path(
        APP_SETTINGS.EXPORT_FILE_PATH,
        ".".join([file_name, APP_SETTINGS.EXPORT_FILE_EXT]),
    )
    audio_data = ((audio_data / 1.414) * 32767).astype(np.int16)

    if APP_SETTINGS.EXPORT_FILE_EXT == "wav":
        # save as wav 16-Bit PCM
        write(file_path, 22050, audio_data)

    elif APP_SETTINGS.EXPORT_FILE_EXT == "ogg":
        # save as ogg
        AudioSegment(
            audio_data.tobytes(),
            sample_width=2,
            frame_rate=22050,
            channels=1,
        ).export(file_path, format="ogg")
    else:
        logging.error(f"Unrecognized File Extension {APP_SETTINGS.EXPORT_FILE_EXT}")
