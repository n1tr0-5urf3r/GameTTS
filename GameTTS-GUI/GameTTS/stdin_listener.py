import sys
import threading, queue

import concurrent.futures
import time
from datetime import datetime
from vits.synthesizer import Synthesizer
from utils.io import parse_csv, parse_json, prepare_for_queue, save_audio
from utils.config import PATHS, APP_SETTINGS

# init logging
import logging

logging.root.handlers = []
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [p%(process)s] [%(filename)s:%(lineno)d] (%(levelname)s: %(message)s)",
    handlers=[logging.FileHandler(PATHS.LOGGING_PATH), logging.StreamHandler()],
)

input_queue = queue.Queue()

try:
    synthesizer = Synthesizer(PATHS.TTS_CONFIG_PATH, PATHS.TTS_MODEL_PATH)
    logging.info("Started Skript and loaded TTS model.")
except Exception as err:
    logging.error(err)


def call_synthesizer(data: dict):
    """TODO


    Args:
        data (dict): Contains information for the synthesizer
    """
    try:
        filename = ".".join(
            [data["FileName"] + "-" + datetime.now().strftime("%S%f"), "wav"]
        )
        # filename = data["FileName"]
        speaker_id = data["SpeakerID"]
        text = data["InputText"]

        audio_data = synthesizer.inference(
            str(text),
            speaker_id,
            {"speech_speed": 1.1, "speech_var_a": 0.345, "speech_var_b": 0.4},
        )

        save_audio(file_name=filename, audio_data=audio_data)
    except Exception as err:
        logging.error(err)


def queue_worker():
    """
    This worker processes incoming jobs in a separate thread.
    """
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:

        future_to_job = {}

        while True:

            # sleep to reduce cpu usage
            time.sleep(0.05)

            # check for status of the futures which are currently working
            done, not_done = concurrent.futures.wait(
                future_to_job,
                return_when=concurrent.futures.FIRST_COMPLETED,
            )

            # if there is incoming work, start a new future
            while not input_queue.empty():

                # fetch a job from the queue
                job = input_queue.get()

                # Start the load operation and mark the future with its job
                future_to_job[executor.submit(call_synthesizer, job)] = job

            # process any completed futures
            for future in done:
                job = future_to_job[future]
                try:
                    data = future.result()
                except Exception as err:
                    logging.error("%r generated an exception: %s" % (job, err))
                finally:
                    del future_to_job[future]


def process_task(json_obj: str) -> str:
    """[summary]

    Args:
        in_data (str): [description]

    Returns:
        str: [description]
    """
    if json_obj["Task"] == "exit":
        sys.exit(0)
    elif json_obj["Task"] == "synth_csv":
        batch_data = parse_csv(json_obj)
        for data in batch_data:
            input_queue.put(data)
    elif json_obj["Task"] == "synth_text":
        data = prepare_for_queue(json_obj=json_obj)
        input_queue.put(data)
    else:
        raise ValueError("Command not found.")


wk_thread = threading.Thread(target=queue_worker, daemon=True).start()

json_input = '{"Task": "synth_text", "SpeakerID": 44, "InputText": "Das ist ein Test.","FileName": "tmp_file_44", "CsvPath": null }'
# main loop: stuff input in the queue
try:
    for std_input in sys.stdin:

        json_obj = parse_json(json_input)

        if json_obj:
            process_task(json_obj)

        # if json_object:
        #     process_input_data(json_object["Batch"])
        # else:
        #     logging.error("invalid input")

        sys.stdout.flush()

    print("All task requests sent\n", end="")

except (Exception, KeyboardInterrupt, SystemExit) as err:
    logging.error(err)
finally:
    # block until all tasks are done
    # input_queue.join()
    print("All work completed")
