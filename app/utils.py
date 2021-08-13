import gdown
import platform
from pathlib import Path
from scipy.io.wavfile import write
from pydub import AudioSegment
import numpy as np

# init paths
APP_FOLDER = Path(Path(__name__).parent.resolve())
APP_CONFIG_PATH = Path(APP_FOLDER, "static_web/resource/", "app-config.json")
SPEAKER_CONFIG = Path(
    APP_FOLDER, "static_web/resource/json-mapping/", "speaker_map.json"
)
TTS_CONFIG_PATH = Path(APP_FOLDER, "vits/model", "config.json")
TTS_MODEL_PATH = Path(APP_FOLDER, "vits/model", "G_800000.pth")


MODEL_URLS = {
    "G_800000.pth": "https://drive.google.com/uc?id=1fTyyVcC76m4YhCSrlpG5dYnwNC_h1KFB",
    "G_900000.pth": "https://drive.google.com/uc?id=1JPHEnM-aho0HRUneA8NBeVG-JnvkRB0s",

    
}

# init platform
PLATFORM = platform.system()

if PLATFORM == "Windows":
    import winsound
elif PLATFORM == "Linux" or PLATFORM == "Darwin":
    from pydub import AudioSegment
    from pydub.playback import play
else:
    print("Unidentified system")


def download_model(model_name):
    url = MODEL_URLS[model_name]
    gdown.download(url, str(TTS_MODEL_PATH), quiet=False)


def create_samples(synthesizer):
    sentence = "So hört sich meine Stimme an."
    for name, idx in synthesizer.speaker_map.items():
        audio_data = synthesizer.synthesize(
            str(sentence),
            idx,
            {"speech_speed": 1.1, "speech_var_a": 0.345, "speech_var_b": 0.5},
        )
        print(name)
        tmp_file_path = Path("static_web", "resource", "audio-samples", idx + ".wav")
        write(tmp_file_path, 22050, audio_data)


def save_audio(out_path, file_name, audio_data, file_ext="wav"):

    file_path = Path(out_path, ".".join([file_name, file_ext]))
    audio_data = ((audio_data / 1.414) * 32767).astype(np.int16)

    if file_ext == "wav":
        # save as wav 16-Bit PCM
        write(file_path, 22050, audio_data)

    elif file_ext == "ogg":
        # save as ogg
        AudioSegment(
            audio_data.tobytes(),
            sample_width=2,
            frame_rate=22050,
            channels=1,
        ).export(file_path, format="ogg")
    else:
        raise f"Unrecognized File Extension  {file_ext}"

    return file_path
