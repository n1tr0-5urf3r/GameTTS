using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace GameTTS_GUI
{
    /// <summary>
    /// A class to collect all data to be used by the UI.
    /// </summary>
    internal class MainData
    {
        #region -- static data

        /// <summary>
        /// Full game/speaker map as read from json file.
        /// </summary>
        public Dictionary<string, Dictionary<string, int>> VoiceMapping { get; private set; }
        /// <summary>
        /// List of games to group voices.
        /// </summary>
        public string[] GameList { get; private set; }
        /// <summary>
        /// List of voices per game (game index serves as array index).
        /// </summary>
        public List<string>[] VoiceLists { get; private set; }
        /// <summary>
        /// Sound player instance to play voice lines from.
        /// </summary>
        public SoundPlayer Player { get; private set; } = new SoundPlayer();

        #endregion

        /// <summary>
        /// List that holds all (finished or unfinished) synthesizer jobs. 
        /// </summary>
        public List<VoiceLine> OutputListData { get; private set; } = new List<VoiceLine>();
        /// <summary>
        /// Current settings for the synthesizer.
        /// </summary>
        public LineSettings Settings { get; private set; } = new LineSettings();

        internal MainData()
        {
            //load json voice mapping
            VoiceMapping = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>
                (File.ReadAllText(Config.SpeakerMapPath));

            GameList = VoiceMapping.Keys.ToArray();
            VoiceLists = new List<string>[GameList.Length];
            int i = 0;
            foreach (var key in VoiceMapping.Keys)
            {
                VoiceLists[i] = VoiceMapping[key].Keys.ToList();
                ++i;
            }
        }
    }
}
