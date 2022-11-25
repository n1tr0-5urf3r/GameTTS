from app.utils import *

from datetime import datetime
from pathlib import Path
import json
import random
from typing import List

from app.utils import *
import os
import discord
from discord import app_commands
import wave
import asyncio

try:
    from vits.synthesizer import Synthesizer
    synthesizer = Synthesizer(TTS_CONFIG_PATH)
    if TTS_MODEL_PATH.exists():
        synthesizer.load_model(TTS_MODEL_PATH)
    else:
        download_model("G_600000.pth")
        synthesizer.load_model(TTS_MODEL_PATH)
    synthesizer.init_speaker_map(SPEAKER_CONFIG)
except ImportError as err:
    eel.call_torch_modal()  # call javascript modal if torch not available

GUILDS = [discord.Object(id=0)]
# Enter token here
TOKEN = ""

class MyClient(discord.Client):
    def __init__(self, *, intents: discord.Intents):
        super().__init__(intents=intents)
        # A CommandTree is a special type that holds all the application command
        # state required to make it work. This is a separate class because it
        # allows all the extra state to be opt-in.
        # Whenever you want to work with application commands, your tree is used
        # to store and work with them.
        # Note: When using commands.Bot instead of discord.Client, the bot will
        # maintain its own tree instead.
        self.tree = app_commands.CommandTree(self)

    # In this basic example, we just synchronize the app commands to one guild.
    # Instead of specifying a guild to every command, we copy over our global commands instead.
    # By doing so, we don't have to wait up to an hour until they are shown to the end-user.
    async def setup_hook(self):
        # This copies the global commands over to your guild.
        for g in GUILDS:
            self.tree.copy_global_to(guild=g)
            await self.tree.sync(guild=g)


intents = discord.Intents.default()
client = MyClient(intents=intents)

def get_speakers():
    games = {0: "gothic", 1: "risen", 2: "skyrim", 3: "witcher"}
    speakers = []
    for i in range(0, 4):
        with open(f'static_web/resource/json-mapping/{i}_sorted.json', 'r') as f:
            data = json.load(f)
        for k, v in data.items():
            speakers.append({"name": f'{k} ({games[i].capitalize()}, {v})', "value": int(v)})
    return speakers

async def speaker_autocomplete(
    interaction: discord.Interaction,
    current: str,
) -> List[app_commands.Choice[str]]:
    speakers = get_speakers()
    return [
        app_commands.Choice(name=s['name'], value=s['value'])
        for s in speakers if current.lower() in s['name'].lower()
    ][:25]

@client.event
async def on_ready():
    print(f'Logged in as {client.user} (ID: {client.user.id})')
    print('------')

@client.event
async def on_voice_state_update(member, before, after):
    if member.id != client.user.id and before.channel is None and after.channel is not None:
        speaker = random.randint(0,132)
        audiopath = synthesize(f"Hallo {member.display_name}, willkommen im {after.channel.name} Kanal!", speaker, "namenloser_held", params_speech)
        channel = after.channel
        await play_in_channel(audiopath, channel, after.channel.guild)
        os.remove(str(audiopath))

@client.tree.command(name="tts", description="Generate Text-To-Speech Output")
@app_commands.describe(speaker="Voice")
@app_commands.autocomplete(speaker=speaker_autocomplete)
async def tts(interaction: discord.Interaction, text: str, speaker: int=47, speech_var_a: float=0.3, speech_var_b: float=0.5, speech_speed: float=1.3):
    try:
        assert speech_var_a > 0 and speech_var_a < 1.0 and speech_var_b > 0 and speech_var_b < 1.0 and speech_speed > 0 and speech_speed <= 2.
    except AssertionError:
        return await interaction.response.send_message("Speech Var must be between 0 and 1, speed between 0 and 2!", ephemeral=True)
    params_speech = {"speech_var_a": speech_var_a, "speech_var_b": speech_var_b, "speech_speed": speech_speed}
    await interaction.response.send_message("Verarbeite...")
    channel = client.get_channel(interaction.channel_id)
    try:
        audiopath = synthesize(text, speaker, "namenloser_held", params_speech)
    except ValueError:
        speaker = 47
        audiopath = synthesize(text, speaker, "namenloser_held", params_speech)
    await interaction.edit_original_response(content="Fertig!")
    await channel.send(file=discord.File(str(audiopath)))
    voice = interaction.user.voice.channel
    await play_in_channel(audiopath, voice, interaction.guild)
    os.remove(str(audiopath))

async def play_in_channel(audiopath, channel, guild):
    try:
        with wave.open(str(audiopath)) as mywav:
            duration_seconds = mywav.getnframes() / mywav.getframerate()
        await channel.connect()
        voice_client: discord.VoiceClient = discord.utils.get(client.voice_clients, guild=guild)
        audio_source = discord.FFmpegPCMAudio(str(audiopath))
        voice_client.play(audio_source, after=None)
        await asyncio.sleep(duration_seconds + 1)
        await voice_client.disconnect()
    except (discord.ext.commands.errors.CommandInvokeError, AttributeError):
        pass

def synthesize(text, speaker_id, speaker_name, params):
    audio_data = synthesizer.synthesize(text, speaker_id, params)
    cur_timestamp = datetime.now().strftime("%m%d%f")
    tmp_path = Path("static_web", "tmp")

    if not tmp_path.exists():
        tmp_path.mkdir()

    file_name = "_".join(
        [str(speaker_id), speaker_name, str(cur_timestamp), "tmp_file"])
    return save_audio(tmp_path, file_name, audio_data)

client.run(TOKEN)
