# GameTTS


## Installation
  
- Download the ZIP folder from [releases](https://github.com/lexkoro/GameTTS/releases/latest/) and extract it
- Run the GameTTS.exe, it should install the required python dependencies and download the TTS model

If successful, the application will start automatically.


***The first start of the application may take a while since the TTS model has to be downloaded first (approx. 155 MB).***


![2021-07-13 20_58_34-Text-To-Speech GUI](https://user-images.githubusercontent.com/6319070/125511688-8c2aed42-d8ac-4826-bf57-fb2bfe27f0fb.png)

## Discord Bot Setup

- Register a [discord application](https://discord.com/developers/applications) and save the bot token
- Install additional dependencies with `pip install -r requirements.txt`
- Install ffmpeg, i.e. with `apt install ffmpeg`
- Start the bot with `python3 discordbot.py`
- Invite the bot to your server
- Check permissions for the bot: It needs permission to send messages, embed links, attach files, read message history, connect to voice and speak in voice. Also channel permissions need to be set up accordingly.

### Features
- `$tts` command: Give a text (quoted with ") and an optional speaker id
- `$speakers` command: Sends you a list with all available speaker ids, sorted per game. Optional parameter: `game` (one of gothic, risen, skyrim, witcher)
- Greet users: Users joining a voice channel on a server the bot is in and has permission to speak, it will join the voice channel, say `Hallo <name>, willkommen im <channel> Kanal!`

## References

- TTS Repository: https://github.com/jaywalnut310/vits
- https://github.com/mdbootstrap


## License and Disclaimer

The released models are made available for non-commercial use only under the terms of the Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0) license. For details, see: https://creativecommons.org/licenses/by-nc/4.0/legalcode
