from pathlib import Path

#######################
#    INITIAL PATHS    #
#######################
class PATHS:
    APP_FOLDER = Path(Path(__name__).parent.resolve())
    TTS_CONFIG_PATH = Path(APP_FOLDER, "vits/model/GruutModel", "config.json")
    TTS_MODEL_PATH = Path(APP_FOLDER, "vits/model/GruutModel", "G_490000.pth")
    LOGGING_PATH = Path(APP_FOLDER, "log/", "debug.log")


#######################
#     APP SETTINGS    #
#######################
class APP_SETTINGS:
    EXPORT_FILE_ENABLED = False
    EXPORT_FILE_PATH = "temp_output"
    EXPORT_FILE_EXT = "wav"


#######################
#  SPEECH SETTINGS    #
#######################
class SPEECH_SETTINGS:
    SPEECH_SPEED = 1.1
    SPEECH_VAR_A = 0.345
    SPEECH_VAR_B = 0.4
