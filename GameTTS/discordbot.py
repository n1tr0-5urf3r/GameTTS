from app.utils import *

from datetime import datetime
from pathlib import Path
import json
import random

from app.utils import *
import os
import discord
from discord.ext import commands
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


# ENTER TOKEN HERE
TOKEN = ""
bot = commands.Bot(description="Namenloser Held", command_prefix="$")
params_speech = {"speech_var_a": 0.3, "speech_var_b": 0.5, "speech_speed": 1.3}


@bot.event
async def on_ready():
    print('We have logged in as {0.user}'.format(bot))

@bot.event
async def on_voice_state_update(member, before, after):
    if member != bot.user and before.channel is None and after.channel is not None:
        speaker = random.randint(0,132)
        audiopath = synthesize(f"Hallo {member.display_name}, willkommen im {after.channel.name} Kanal!", speaker, "namenloser_held", params_speech)
        channel = after.channel
        await play_in_channel(audiopath, channel, after.channel.guild)
        os.remove(str(audiopath))

@bot.command(pass_context=True)
async def tts(ctx, text, speaker=47):
    msg = await ctx.send("Verarbeite....")
    async with ctx.typing():
        try:
            audiopath = synthesize(text, speaker, "namenloser_held", params_speech)
        except ValueError:
            speaker = 47
            audiopath = synthesize(text, speaker, "namenloser_held", params_speech)
        await msg.edit(content="Fertig!")
        await ctx.send(file=discord.File(str(audiopath)))
    voice = ctx.message.author.voice.channel
    await play_in_channel(audiopath, voice, ctx.guild)
    os.remove(str(audiopath))

@bot.command(pass_context=True)
async def speakers(ctx, game=None):
    games = {"gothic": "0", "risen": "1", "skyrim": "2", "witcher": "3"}
    if game and game.lower() in games.keys():
        with open(f'static_web/resource/json-mapping/{games[game]}_sorted.json', 'r') as f:
            data = json.load(f)
        txt = ""
        for k, v in data.items():
            txt += f"{k}: {v}\n"
        await ctx.send(f"**{game.capitalize()}**```{txt}```")
    else:
        for i in range(0, 4):
            with open(f'static_web/resource/json-mapping/{i}_sorted.json', 'r') as f:
                data = json.load(f)
            txt = ""
            for k, v in data.items():
                txt += f"{k}: {v}\n"
            await ctx.send(f"**{list(games.keys())[list(games.values()).index(str(i))].capitalize()}**```{txt}```")

async def play_in_channel(audiopath, channel, guild):
    try:
        with wave.open(str(audiopath)) as mywav:
            duration_seconds = mywav.getnframes() / mywav.getframerate()
        await channel.connect()
        voice_client: discord.VoiceClient = discord.utils.get(bot.voice_clients, guild=guild)
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

bot.run(TOKEN)
